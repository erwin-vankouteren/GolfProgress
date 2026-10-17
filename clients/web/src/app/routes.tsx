import { lazy, Suspense, type ReactNode } from "react";
import type { RouteObject } from "react-router";
import { CreateAccountPage, SignInPage, WelcomePage } from "../auth/AuthPages";
import { ForgotPasswordPage, ResetPasswordPage } from "../auth/PasswordResetPages";
import { Skeleton } from "../common/states";
import { AppLayout } from "../layout/AppLayout";
import { DashboardPage } from "../dashboard/DashboardPage";
import { NotFoundPage } from "../pages/SectionPage";
import { LogPracticePage, PracticeDetailPage, PracticePage } from "../practice/PracticePages";
import { LogRoundPage } from "../rounds/LogRound";
import { EditRoundPage, RoundDetailPage } from "../rounds/RoundDetailPage";
import { RoundsLayout } from "../rounds/RoundsLayout";
import { PublicOnly, RequireSession } from "../session/SessionContext";

// Loaded on first visit, inside the app shell: Progress (#107) for its charts, and the
// profile pages (#111), which are seldom opened. The main bundle stays under 500 kB.
const ProgressPage = lazy(() => import("../progress/ProgressPage").then((m) => ({ default: m.ProgressPage })));
const ProfilePage = lazy(() => import("../profile/ProfilePage").then((m) => ({ default: m.ProfilePage })));
const OnboardingPage = lazy(() => import("../profile/OnboardingPage").then((m) => ({ default: m.OnboardingPage })));

const loading = (page: ReactNode) => <Suspense fallback={<Skeleton blocks={3} />}>{page}</Suspense>;

/**
 * The app's routes (#75, #69). The sign-in screens are only for signed-out users; the
 * app itself requires a session, and sends anyone else to Welcome.
 */
export const routes: RouteObject[] = [
  // From the reset email (#86): open whether or not someone is signed in on this browser.
  { path: "reset-password", element: <ResetPasswordPage /> },
  {
    element: <PublicOnly />,
    children: [
      { path: "welcome", element: <WelcomePage /> },
      { path: "sign-in", element: <SignInPage /> },
      { path: "create-account", element: <CreateAccountPage /> },
      { path: "forgot-password", element: <ForgotPasswordPage /> },
    ],
  },
  {
    element: <RequireSession />,
    children: [
      // After Create account, full screen like the sign-in screens (#111).
      { path: "onboarding", element: loading(<OnboardingPage />) },
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <DashboardPage /> },
          {
            path: "rounds",
            element: <RoundsLayout />,
            children: [
              { path: "new", element: <LogRoundPage /> },
              { path: ":roundId", element: <RoundDetailPage /> },
              { path: ":roundId/edit", element: <EditRoundPage /> },
            ],
          },
          { path: "practice", element: <PracticePage /> },
          { path: "practice/new", element: <LogPracticePage /> },
          { path: "practice/:sessionId", element: <PracticeDetailPage /> },
          { path: "progress", element: loading(<ProgressPage />) },
          { path: "profile", element: loading(<ProfilePage />) },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
  },
];
