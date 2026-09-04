import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { GoogleOAuthProvider } from '@react-oauth/google'
import './index.css'
import App from './App.tsx'

// VITE_GOOGLE_CLIENT_ID must be set in your .env.local file
// Example: VITE_GOOGLE_CLIENT_ID=your-client-id.apps.googleusercontent.com
const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '';

async function bootstrap() {
  if (import.meta.env.VITE_ENABLE_API_MOCKS === 'true') {
    const { enableApiMocks } = await import('./mocks/mockApi.ts');
    enableApiMocks();
  }

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <GoogleOAuthProvider clientId={googleClientId}>
        <App />
      </GoogleOAuthProvider>
    </StrictMode>,
  );
}

void bootstrap();
