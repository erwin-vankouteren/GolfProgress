import type { RouteObject } from "react-router";
import { CreateAccountPage, SignInPage, WelcomePage } from "../auth/AuthPages";
import { AppLayout } from "../layout/AppLayout";
import { DashboardPage } from "../dashboard/DashboardPage";
import { NotFoundPage, PracticePage, ProgressPage, RoundsPage } from "../pages/SectionPage";
import { ProfilePage } from "../pages/ProfilePage";
import { LogRoundPage } from "../rounds/LogRound";
import { RoundDetailPage } from "../rounds/RoundDetailPage";
import { PublicOnly, RequireSession } from "../session/SessionContext";

/**
 * The app's routes (#75, #69). The sign-in screens are only for signed-out users; the
 * app itself requires a session, and sends anyone else to Welcome.
 */
export const routes: RouteObject[] = [
  {
    element: <PublicOnly />,
    children: [
      { path: "welcome", element: <WelcomePage /> },
      { path: "sign-in", element: <SignInPage /> },
      { path: "create-account", element: <CreateAccountPage /> },
    ],
  },
  {
    element: <RequireSession />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <DashboardPage /> },
          { path: "rounds", element: <RoundsPage /> },
          { path: "rounds/new", element: <LogRoundPage /> },
          { path: "rounds/:roundId", element: <RoundDetailPage /> },
          { path: "practice", element: <PracticePage /> },
          { path: "progress", element: <ProgressPage /> },
          { path: "profile", element: <ProfilePage /> },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
  },
];
