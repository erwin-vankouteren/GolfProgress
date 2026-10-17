import { lazy, Suspense } from "react";
import type { RouteObject } from "react-router";
import { CreateAccountPage, SignInPage, WelcomePage } from "../auth/AuthPages";
import { Skeleton } from "../common/states";
import { AppLayout } from "../layout/AppLayout";
import { DashboardPage } from "../dashboard/DashboardPage";
import { NotFoundPage } from "../pages/SectionPage";
import { LogPracticePage, PracticeDetailPage, PracticePage } from "../practice/PracticePages";
import { ProfilePage } from "../pages/ProfilePage";
import { LogRoundPage } from "../rounds/LogRound";
import { EditRoundPage, RoundDetailPage } from "../rounds/RoundDetailPage";
import { RoundsLayout } from "../rounds/RoundsLayout";
import { PublicOnly, RequireSession } from "../session/SessionContext";

// Loaded on first visit, inside the app shell: its charts are most of its weight (#107).
const ProgressPage = lazy(() => import("../progress/ProgressPage").then((m) => ({ default: m.ProgressPage })));

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
          {
            path: "progress",
            element: (
              <Suspense fallback={<Skeleton blocks={3} />}>
                <ProgressPage />
              </Suspense>
            ),
          },
          { path: "profile", element: <ProfilePage /> },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
  },
];
