export function resolveProfileTab(pathname: string, search: string, role: string): 'profile' | 'mentor' | 'password' {
  if (pathname === '/settings') return 'password';
  if (role.trim().toUpperCase() === 'MENTOR' && new URLSearchParams(search).get('tab') !== 'personal') return 'mentor';
  return 'profile';
}
