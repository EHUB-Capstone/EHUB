import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

interface ProtectedRouteProps {
  /** If provided, user must have at least one of these roles */
  allowedRoles?: string[];
  /** Where to redirect unauthenticated users (default: /login) */
  redirectTo?: string;
  /** Supports WDP's nested route wrapper pattern. */
  children?: React.ReactNode;
}

function normalizeRole(role: string): string {
  return role.trim().toUpperCase();
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({
  allowedRoles,
  redirectTo = '/login',
  children,
}) => {
  const { isAuthenticated, isLoading, user } = useAuth();

  // Show a minimal full-screen spinner while verifying token on first load
  if (isLoading) {
    return (
      <div style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: '#0F172A',
      }}>
        <div style={{
          width: 36,
          height: 36,
          borderRadius: '50%',
          border: '3px solid rgba(234,106,18,0.25)',
          borderTopColor: '#EA6A12',
          animation: 'spin 0.8s linear infinite',
        }} />
        <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to={redirectTo} replace />;
  }

  // Role-based guard
  if (allowedRoles && allowedRoles.length > 0 && user) {
    const allowedRoleSet = new Set(allowedRoles.map(normalizeRole));
    const hasRole = user.roles.some(r => allowedRoleSet.has(normalizeRole(r)));
    if (!hasRole) {
      return <Navigate to="/403" replace />;
    }
  }

  return children ? <>{children}</> : <Outlet />;
};
