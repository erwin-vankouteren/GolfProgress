using GolfProgress.Api.Configuration;
using GolfProgress.Api.Services.Payments;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using Stripe.Checkout;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #310: subscribing on the web with Stripe. Stripe's API is a fake; its webhook
/// signatures are checked for real, with events signed here as Stripe signs them.
/// </summary>
public sealed class StripeSubscriptionTests : IAsyncLifetime
{
    private const string WebhookSecret = "whsec_test_secret";
    private const string Password = "Correct-horse-1";

    private readonly ApiFactory _base = new();
    private readonly FakeStripe _stripe = new();
    private WebApplicationFactory<Program> _factory = null!;
    private readonly Guid _annId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _factory = _base.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.PostConfigure<PaymentsOptions>(o =>
            {
                o.WebAppUrl = "https://app.example.test";
                o.Stripe = new StripeOptions
                {
                    SecretKey = "sk_test_x",
                    WebhookSecret = WebhookSecret,
                    PremiumMonthPrice = "price_pm",
                    PremiumYearPrice = "price_py",
                    CoachingMonthPrice = "price_cm",
                    CoachingYearPrice = "price_cy",
                };
            });
            s.AddSingleton<IStripeGateway>(_stripe);
        }));

        await _base.WithDbAsync(async db =>
        {
            var ann = TestWorld.NewUser(_annId, "ann@example.test");
            using var scope = _base.Services.CreateScope();
            ann.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(ann, Password);
            db.Users.Add(ann);
            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        _base.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Prices_ComeFromStripe_AndAre503UntilItsSetUp()
    {
        var prices = await Ann().GetFromJsonAsync<JsonElement>("/subscription/prices");
        Assert.Equal("eur", prices.GetProperty("currency").GetString());
        Assert.Equal(299, prices.GetProperty("premiumMonth").GetInt64());
        Assert.Equal(1900, prices.GetProperty("coachingYear").GetInt64());

        var unconfigured = TestWorld.ClientFor(_base, _annId);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await unconfigured.GetAsync("/subscription/prices")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await unconfigured.PostAsJsonAsync("/me/subscription/checkout", new { period = "Month" })).StatusCode);
    }

    [Fact]
    public async Task Checkout_IsForTheUser_WithCoachingWhenAsked_AndReusesTheirCustomer()
    {
        var first = await Ann().PostAsJsonAsync("/me/subscription/checkout", new { period = "Year", coaching = true });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(FakeStripe.CheckoutUrl, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString());

        var options = Assert.Single(_stripe.Checkouts);
        Assert.Equal("subscription", options.Mode);
        Assert.Equal("cus_1", options.Customer);
        Assert.Equal(["price_py", "price_cy"], options.LineItems.Select(i => i.Price));
        Assert.Equal(_annId.ToString(), options.SubscriptionData.Metadata["userId"]);
        Assert.Equal("https://app.example.test/profile?subscription=done", options.SuccessUrl);
        Assert.Equal("required", options.ConsentCollection.TermsOfService);
        // Off unless asked for: the prices are what customers pay.
        Assert.False(options.AutomaticTax.Enabled);
        Assert.Null(options.CustomerUpdate);

        await Ann().PostAsJsonAsync("/me/subscription/checkout", new { period = "Month" });
        Assert.Equal(["price_pm"], _stripe.Checkouts[1].LineItems.Select(i => i.Price));
        // One Stripe customer per user.
        Assert.Equal(1, _stripe.CustomersCreated);
    }

    [Fact]
    public async Task Checkout_NeedsAKnownPeriod()
    {
        var response = await Ann().PostAsJsonAsync("/me/subscription/checkout", new { period = "Week" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_stripe.Checkouts);
    }

    [Fact]
    public async Task TheWebhook_RecordsTheSubscription_AndMeShowsIt()
    {
        await LinkCustomerAsync();
        _stripe.Subscriptions["sub_1"] = Subscription("active", ("price_pm", 30), ("price_cm", 30));

        Assert.Equal(HttpStatusCode.OK, (await SendWebhookAsync("customer.subscription.created", "sub_1")).StatusCode);

        var me = await Ann().GetFromJsonAsync<JsonElement>("/me");
        Assert.Equal("Subscribed", me.GetProperty("plan").GetString());
        var subscription = me.GetProperty("subscription");
        Assert.Equal("Stripe", subscription.GetProperty("source").GetString());
        Assert.Equal("Month", subscription.GetProperty("period").GetString());
        Assert.True(subscription.GetProperty("coachingAddOn").GetBoolean());

        // The same event again changes nothing.
        await SendWebhookAsync("customer.subscription.created", "sub_1");
        await _base.WithDbAsync(async db => Assert.Equal(2, await db.Subscriptions.CountAsync(s => s.UserId == _annId)));
    }

    [Fact]
    public async Task CompletingCheckout_RecordsItsSubscription()
    {
        await LinkCustomerAsync();
        _stripe.Subscriptions["sub_1"] = Subscription("active", ("price_py", 365));

        await SendWebhookAsync("checkout.session.completed", "cs_1",
            """{"id":"cs_1","object":"checkout.session","mode":"subscription","subscription":"sub_1"}""");

        await _base.WithDbAsync(async db =>
        {
            var row = await db.Subscriptions.SingleAsync(s => s.UserId == _annId);
            Assert.Equal(SubscriptionProduct.Premium, row.Product);
            Assert.Equal(SubscriptionPeriod.Year, row.Period);
        });
    }

    [Fact]
    public async Task Changes_InThePortal_FollowOnTheNextEvent()
    {
        await LinkCustomerAsync();
        _stripe.Subscriptions["sub_1"] = Subscription("active", ("price_pm", 30), ("price_cm", 30));
        await SendWebhookAsync("customer.subscription.created", "sub_1");

        // Coaching removed, cancelled at the period's end, and a payment failing meanwhile.
        var changed = Subscription("past_due", ("price_pm", 30));
        changed.CancelAtPeriodEnd = true;
        _stripe.Subscriptions["sub_1"] = changed;
        await SendWebhookAsync("customer.subscription.updated", "sub_1");

        var me = await Ann().GetFromJsonAsync<JsonElement>("/me");
        Assert.Equal("Subscribed", me.GetProperty("plan").GetString());
        var subscription = me.GetProperty("subscription");
        Assert.False(subscription.GetProperty("coachingAddOn").GetBoolean());
        Assert.Equal(JsonValueKind.Null, subscription.GetProperty("renewsAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, subscription.GetProperty("cancelsAt").ValueKind);

        _stripe.Subscriptions["sub_1"] = Subscription("canceled", ("price_pm", 0));
        await SendWebhookAsync("customer.subscription.deleted", "sub_1");
        Assert.Equal(JsonValueKind.Null, (await Ann().GetFromJsonAsync<JsonElement>("/me")).GetProperty("subscription").ValueKind);
    }

    [Fact]
    public async Task AnUnsignedOrWronglySignedWebhook_Is400_AndChangesNothing()
    {
        await LinkCustomerAsync();
        _stripe.Subscriptions["sub_1"] = Subscription("active", ("price_pm", 30));

        Assert.Equal(HttpStatusCode.BadRequest, (await SendWebhookAsync("customer.subscription.created", "sub_1", secret: "whsec_other")).StatusCode);
        var unsigned = await _factory.CreateClient().PostAsync("/webhooks/stripe",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, unsigned.StatusCode);

        await _base.WithDbAsync(async db => Assert.False(await db.Subscriptions.AnyAsync()));
    }

    [Fact]
    public async Task ASubscriber_CantCheckOutAgain_ButOpensThePortal()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Ann().PostAsync("/me/subscription/portal", null)).StatusCode);

        await LinkCustomerAsync();
        _stripe.Subscriptions["sub_1"] = Subscription("active", ("price_pm", 30));
        await SendWebhookAsync("customer.subscription.created", "sub_1");

        Assert.Equal(HttpStatusCode.Conflict,
            (await Ann().PostAsJsonAsync("/me/subscription/checkout", new { period = "Month" })).StatusCode);
        var portal = await Ann().PostAsync("/me/subscription/portal", null);
        Assert.Equal(FakeStripe.PortalUrl, (await portal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString());
    }

    [Fact]
    public async Task DeletingTheAccount_CancelsTheStripeSubscription_OrDeletesNothingIfItCant()
    {
        await LinkCustomerAsync();
        _stripe.Subscriptions["sub_1"] = Subscription("active", ("price_pm", 30));
        await SendWebhookAsync("customer.subscription.created", "sub_1");

        _stripe.CancelFails = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await DeleteAnnAsync()).StatusCode);
        await _base.WithDbAsync(async db => Assert.True(await db.Users.AnyAsync(u => u.UserId == _annId)));

        _stripe.CancelFails = false;
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAnnAsync()).StatusCode);
        Assert.Equal(["sub_1"], _stripe.Cancelled);
        await _base.WithDbAsync(async db => Assert.False(await db.Subscriptions.AnyAsync()));
    }

    private Task<HttpResponseMessage> DeleteAnnAsync() =>
        Ann().SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/me") { Content = JsonContent.Create(new { password = Password }) });

    private Task LinkCustomerAsync() => _base.WithDbAsync(async db =>
    {
        (await db.Users.SingleAsync(u => u.UserId == _annId)).StripeCustomerId = "cus_1";
        await db.SaveChangesAsync();
    });

    private Stripe.Subscription Subscription(string status, params (string Price, int Days)[] items) => new()
    {
        Id = "sub_1",
        CustomerId = "cus_1",
        Status = status,
        Metadata = new Dictionary<string, string> { ["userId"] = _annId.ToString() },
        Items = new StripeList<SubscriptionItem>
        {
            Data = items.Select(i => new SubscriptionItem
            {
                Price = new Price { Id = i.Price },
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(i.Days),
            }).ToList(),
        },
    };

    /// <summary>Posts an event to the webhook, signed as Stripe signs them (t=…,v1=HMAC-SHA256).</summary>
    private async Task<HttpResponseMessage> SendWebhookAsync(string type, string objectId, string? objectJson = null, string secret = WebhookSecret)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = objectJson ?? "{\"id\":\"" + objectId + "\",\"object\":\"subscription\"}";
        var json = "{\"id\":\"evt_" + Guid.NewGuid().ToString("N") + "\",\"object\":\"event\",\"api_version\":\"2025-01-01\",\"created\":"
            + created + ",\"type\":\"" + type + "\",\"data\":{\"object\":" + data + "}}";
        var signature = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{created}.{json}"))).ToLowerInvariant();

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add("Stripe-Signature", $"t={created.ToString(CultureInfo.InvariantCulture)},v1={signature}");
        return await _factory.CreateClient().SendAsync(request);
    }

    private HttpClient Ann()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestWorld.TokenFor(_annId));
        return client;
    }

    /// <summary>Stripe's API in memory; signatures are checked by Stripe's own code.</summary>
    private sealed class FakeStripe : IStripeGateway
    {
        public const string CheckoutUrl = "https://checkout.stripe.test/c/1";
        public const string PortalUrl = "https://billing.stripe.test/p/1";

        public int CustomersCreated { get; private set; }
        public List<SessionCreateOptions> Checkouts { get; } = [];
        public Dictionary<string, Stripe.Subscription> Subscriptions { get; } = [];
        public List<string> Cancelled { get; } = [];
        public bool CancelFails { get; set; }

        public Task<string> CreateCustomerAsync(Guid userId, string? email, string? name, CancellationToken ct) =>
            Task.FromResult($"cus_{++CustomersCreated}");

        public Task<string> CreateCheckoutAsync(SessionCreateOptions options, CancellationToken ct)
        {
            Checkouts.Add(options);
            return Task.FromResult(CheckoutUrl);
        }

        public Task<string> CreatePortalAsync(string customerId, string returnUrl, string? locale, CancellationToken ct) =>
            Task.FromResult(PortalUrl);

        public Task<Stripe.Subscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct) =>
            Task.FromResult(Subscriptions.GetValueOrDefault(subscriptionId));

        public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct)
        {
            if (CancelFails) throw new StripeException("Stripe is down");
            Cancelled.Add(subscriptionId);
            return Task.CompletedTask;
        }

        public Task<(long Amount, string Currency)> GetPriceAsync(string priceId, CancellationToken ct) =>
            Task.FromResult(priceId switch
            {
                "price_pm" => (299L, "eur"),
                "price_py" => (2900L, "eur"),
                "price_cm" => (199L, "eur"),
                _ => (1900L, "eur"),
            });

        public Event ParseWebhook(string json, string signatureHeader, string secret) =>
            EventUtility.ConstructEvent(json, signatureHeader, secret, throwOnApiVersionMismatch: false);
    }
}
