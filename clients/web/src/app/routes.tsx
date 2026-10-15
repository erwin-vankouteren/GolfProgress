import type { RouteObject } from "react-router";
import { AppLayout } from "../layout/AppLayout";
import {
  HomePage,
  LogRoundPage,
  NotFoundPage,
  PracticePage,
  ProfilePage,
  ProgressPage,
  RoundsPage,
} from "../pages/SectionPage";

/** The app's routes (#75). Sign-in routes and the guard around these arrive in #69. */
export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "rounds", element: <RoundsPage /> },
      { path: "rounds/new", element: <LogRoundPage /> },
      { path: "practice", element: <PracticePage /> },
      { path: "progress", element: <ProgressPage /> },
      { path: "profile", element: <ProfilePage /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
];
