import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { requiresMajor } from '../utils/requiresMajor';

export function RequireMajor() {
  const { user, isLoading, isAuthenticated } = useAuth();
  const { pathname } = useLocation();
  if (isLoading) return <div role="status">Loading...</div>;
  const needsMajor = isAuthenticated && requiresMajor(user);
  if (needsMajor && pathname !== '/settings') {
    return <Navigate to="/settings" replace />;
  }
  return <Outlet />;
}
