const readHttpsOrigin = (value) => {
  try {
    const url = new URL(value.trim());
    const isOriginOnly = url.pathname === '/' && !url.search && !url.hash;
    const hasNoCredentials = !url.username && !url.password;

    if (url.protocol === 'https:' && isOriginOnly && hasNoCredentials) {
      return url.origin;
    }
  } catch {
    // A clear deployment error is thrown below.
  }

  throw new Error(
    'API_PROXY_ORIGIN must be set to the HTTPS origin of the deployed backend, for example https://ehub-api-mentor-staging.onrender.com.',
  );
};

const apiProxyOrigin = readHttpsOrigin(process.env.API_PROXY_ORIGIN || '');

export const config = {
  framework: 'vite',
  buildCommand: 'npm run build',
  outputDirectory: 'dist',
  rewrites: [
    {
      source: '/api/:path*',
      destination: `${apiProxyOrigin}/api/:path*`,
    },
    {
      source: '/(.*)',
      destination: '/index.html',
    },
  ],
  headers: [
    {
      source: '/(.*)',
      headers: [
        { key: 'X-Content-Type-Options', value: 'nosniff' },
        { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
        { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
      ],
    },
  ],
};
