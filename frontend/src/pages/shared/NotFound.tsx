// @ts-nocheck
import { SearchX, ArrowLeft, Home } from 'lucide-react';
import Button from '../../components/ui/Button';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';

const NotFound = () => {
  const navigate = useNavigate();
  const { user } = useAuth();

  const handleBack = () => {
    if (!user) navigate('/login');
    else if (user.role === 'ADMIN') navigate('/admin');
    else if (user.role === 'LECTURER') navigate('/lecturer');
    else navigate('/student');
  };

  return (
    <div className="min-h-screen bg-gradient-hero flex items-center justify-center p-6 relative overflow-hidden">
      <div className="absolute top-[-15%] left-[-10%] w-[500px] h-[500px] bg-primary-100/40 rounded-full blur-[120px]" />
      <div className="absolute bottom-[-10%] right-[-5%] w-[400px] h-[400px] bg-secondary-100/30 rounded-full blur-[100px]" />

      <div className="max-w-md w-full text-center relative z-10">
        <div className="bg-white/90 backdrop-blur-xl border border-slate-200/60 rounded-2xl shadow-float p-10">
          <div className="w-20 h-20 bg-slate-100 rounded-2xl flex items-center justify-center mx-auto mb-6">
            <SearchX className="w-10 h-10 text-slate-400" />
          </div>
          <h1 className="text-5xl font-black text-slate-900 tracking-tight mb-2">404</h1>
          <h2 className="text-xl font-bold text-slate-800 mb-3">Page not found</h2>
          <p className="text-body text-slate-500 leading-relaxed mb-8">
            The page you are looking for doesn't exist or has been moved.
          </p>
          <div className="flex gap-3">
            <Button variant="outline" size="lg" className="flex-1" icon={ArrowLeft} onClick={() => navigate(-1)}>
              Go Back
            </Button>
            <Button variant="gradient" size="lg" className="flex-1" icon={Home} onClick={handleBack}>
              Dashboard
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default NotFound;
