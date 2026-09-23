import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { getMajorCompletionRedirect } from '../utils/requiresMajor';

export function RequireMajor() {
  const { user, isLoading, isAuthenticated } = useAuth();
  const { pathname } = useLocation();
  if (isLoading) return <div role="status">Loading...</div>;
  const redirectTo = isAuthenticated ? getMajorCompletionRedirect(user, pathname) : null;
  if (redirectTo) {
    return <Navigate to={redirectTo} replace />;
  }
  return <Outlet />;
}
