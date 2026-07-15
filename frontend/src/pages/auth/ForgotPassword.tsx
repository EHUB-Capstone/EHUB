// @ts-nocheck
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Sparkles, ArrowLeft, Mail, CheckCircle2 } from 'lucide-react';
import Button from '../../components/ui/Button';
import axiosClient from '../../api/axiosClient';
import toast from 'react-hot-toast';

const ForgotPassword = () => {
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!email) return toast.error('Please enter your email');
    setIsLoading(true);
    try {
      await axiosClient.post('/auth/forgot-password', { email });
      setIsSuccess(true);
    } catch (err) {
      toast.error(err.message || 'Failed to process request');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-hero flex items-center justify-center p-6 relative overflow-hidden">
      {/* Decorative blobs */}
      <div className="absolute top-[-15%] left-[-10%] w-[500px] h-[500px] bg-primary-100/40 rounded-full blur-[120px]" />
      <div className="absolute bottom-[-10%] right-[-5%] w-[400px] h-[400px] bg-cyan-100/40 rounded-full blur-[100px]" />
      <div className="absolute top-[30%] right-[15%] w-[250px] h-[250px] bg-secondary-100/30 rounded-full blur-[80px]" />

      <div className="w-full max-w-[440px] relative z-10">
        <div className="bg-white/90 backdrop-blur-xl border border-slate-200/60 rounded-2xl shadow-float p-8 sm:p-10">
          {/* Logo */}
          <div className="text-center mb-8">
            <div className="w-14 h-14 bg-gradient-to-br from-primary to-secondary rounded-2xl flex items-center justify-center mx-auto mb-4 shadow-glow-primary">
              <Sparkles className="w-6 h-6 text-white" />
            </div>
            <h1 className="text-heading font-bold text-slate-900">Forgot Password</h1>
            <p className="text-body text-slate-500 mt-1">Enter your email and we'll send you instructions to reset your password.</p>
          </div>

          {isSuccess ? (
            <div className="flex flex-col items-center text-center space-y-4">
              <div className="w-16 h-16 bg-success-50 rounded-2xl flex items-center justify-center">
                <CheckCircle2 className="w-8 h-8 text-success" />
              </div>
              <h3 className="text-xl font-bold text-slate-900">Check your inbox</h3>
              <p className="text-slate-500 text-body leading-relaxed">
                If an account exists for <span className="font-medium text-slate-900">{email}</span>, we have sent a password reset link.
              </p>
              <Link to="/login" className="w-full mt-4">
                <Button variant="outline" className="w-full">Return to login</Button>
              </Link>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-5">
              <div>
                <label htmlFor="forgot-email" className="block text-caption font-medium text-slate-600 mb-1.5">Email Address</label>
                <div className="relative">
                  <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                  <input
                    id="forgot-email"
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2.5 text-body text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
                  />
                </div>
              </div>

              <Button type="submit" variant="gradient" className="w-full" size="lg" isLoading={isLoading}>
                Send Reset Link
              </Button>
            </form>
          )}
        </div>

        <div className="mt-8 text-center">
          <Link to="/login" className="inline-flex items-center gap-2 text-body font-medium text-slate-500 hover:text-slate-900 transition-colors">
            <ArrowLeft className="w-4 h-4" /> Back to login
          </Link>
        </div>
      </div>
    </div>
  );
};

export default ForgotPassword;
