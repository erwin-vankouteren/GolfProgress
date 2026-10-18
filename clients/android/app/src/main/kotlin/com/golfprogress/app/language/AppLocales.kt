package com.golfprogress.app.language

import android.app.Activity
import android.app.LocaleManager
import android.content.Context
import android.content.ContextWrapper
import android.content.res.Configuration
import android.content.res.Resources
import android.os.Build
import android.os.LocaleList
import androidx.annotation.RequiresApi
import java.util.Locale

/**
 * A language the app comes in. [apiName] is how the API's UserResponse.language names
 * it; [ownName] is how the language names itself, as language pickers show it.
 */
enum class AppLanguage(
    val tag: String,
    val apiName: String,
    val ownName: String,
) {
    English("en", "English", "English"),
    Dutch("nl", "Dutch", "Nederlands"),
    ;

    val locale: Locale get() = Locale.forLanguageTag(tag)

    companion object {
        /** A value this version doesn't know (the API may add languages) reads as no choice. */
        fun fromApi(name: String?): AppLanguage? = entries.firstOrNull { it.apiName == name }

        /** By the language of a tag: nl-NL and nl-BE are both Dutch. */
        fun fromTag(tag: String?): AppLanguage? {
            val language = tag?.lowercase(Locale.ROOT)?.substringBefore('-') ?: return null
            return entries.firstOrNull { it.tag == language }
        }
    }
}

/** The language shown: the one chosen, else the first of the device's the app comes in, else English. */
fun shownLanguage(
    choice: AppLanguage?,
    deviceTags: List<String>,
): AppLanguage = choice ?: deviceTags.firstNotNullOfOrNull(AppLanguage::fromTag) ?: AppLanguage.English

/**
 * The app's language: the one chosen in Profile, or none to follow the device.
 *
 * From Android 13 the system keeps the choice and applies it ([LocaleManager]); it also
 * shows it in the phone's settings for the app, where it can be changed too. Before
 * Android 13 the app keeps it, and [wrap] gives the activity's context that language;
 * changing it recreates the activity.
 */
object AppLocales {
    private const val PREFERENCES = "language"
    private const val CHOICE = "choice"
    private const val SYNCED_FOR = "synced_for"

    fun choice(context: Context): AppLanguage? =
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            localeManager(context).applicationLocales.let { if (it.isEmpty) null else AppLanguage.fromTag(it[0].toLanguageTag()) }
        } else {
            AppLanguage.fromTag(preferences(context).getString(CHOICE, null))
        }

    /** What the app shows now, e.g. for Accept-Language. */
    fun shown(context: Context): AppLanguage = shownLanguage(choice(context), deviceTags(context))

    /** The device's own languages, whatever the app shows. */
    fun deviceTags(context: Context): List<String> {
        val locales =
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                localeManager(context).systemLocales
            } else {
                Resources.getSystem().configuration.locales
            }
        return (0 until locales.size()).map { locales[it].toLanguageTag() }
    }

    /** Switches to [choice] (null: the device's language), if it isn't already. */
    fun apply(
        context: Context,
        choice: AppLanguage?,
    ) {
        if (choice == choice(context)) return
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            // The system recreates the activities in the new language.
            localeManager(context).applicationLocales =
                choice?.let { LocaleList.forLanguageTags(it.tag) } ?: LocaleList.getEmptyLocaleList()
        } else {
            preferences(context).edit().putString(CHOICE, choice?.tag).commit()
            context.findActivity()?.recreate()
        }
    }

    /** Before Android 13, the activity's context in the chosen language; [Activity.attachBaseContext]. */
    fun wrap(base: Context): Context {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) return base
        val choice = choice(base) ?: return base
        val configuration = Configuration(base.resources.configuration).apply { setLocales(LocaleList(choice.locale)) }
        return base.createConfigurationContext(configuration)
    }

    /**
     * The profile's language is taken over once per account on this device, at its first
     * sign-in here (see LanguageSync). After that this device's choice is its own: one
     * made in the phone's settings isn't undone at every launch.
     */
    fun syncedFor(context: Context): String? = preferences(context).getString(SYNCED_FOR, null)

    fun markSynced(
        context: Context,
        userId: String,
    ) {
        preferences(context).edit().putString(SYNCED_FOR, userId).apply()
    }

    private fun preferences(context: Context) = context.applicationContext.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)

    @RequiresApi(Build.VERSION_CODES.TIRAMISU)
    private fun localeManager(context: Context) = context.getSystemService(LocaleManager::class.java)

    private tailrec fun Context.findActivity(): Activity? =
        when (this) {
            is Activity -> this
            is ContextWrapper -> baseContext.findActivity()
            else -> null
        }
}
