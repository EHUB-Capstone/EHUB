import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { ThemeProvider } from './context/ThemeContext';

// Public Pages
import Home from './pages/Home';
import Login from './pages/auth/Login';
import Register from './pages/auth/Register';

const NotFound: React.FC = () => (
  <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#0a0f1e' }}>
    <div style={{ textAlign: 'center' }}>
      <h1 style={{ fontSize: 72, fontWeight: 900, color: '#f1f5f9', marginBottom: 16 }}>404</h1>
      <p style={{ color: '#64748b', marginBottom: 24 }}>Page not found</p>
      <a href="/" style={{ color: '#F37021', fontWeight: 600, textDecoration: 'none' }}>← Back to Home</a>
    </div>
  </div>
);

function App(): React.ReactElement {
  return (
    <ThemeProvider>
      <Router>
        <Toaster
          position="top-right"
          toastOptions={{
            duration: 3500,
            style: {
              borderRadius: '12px',
              fontFamily: 'Inter, sans-serif',
              fontSize: '14px',
              padding: '12px 16px',
              boxShadow: '0 4px 16px -4px rgb(0 0 0 / 0.2)',
            },
            success: { iconTheme: { primary: '#51B848', secondary: '#fff' } },
            error:   { iconTheme: { primary: '#ef4444', secondary: '#fff' } },
          }}
        />
        <Routes>
          <Route path="/"         element={<Home />} />
          <Route path="/login"    element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="*"         element={<NotFound />} />
        </Routes>
      </Router>
    </ThemeProvider>
  );
}

export default App;
