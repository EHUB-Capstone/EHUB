import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, Mail, CheckCircle2 } from 'lucide-react';
import Button from '../../components/ui/Button';
import Input from '../../components/ui/Input';
import { forgotPassword } from '../../api/authApi';
import { parseApiError } from '../../utils/apiError';
import toast from 'react-hot-toast';
import logo from '../../assets/logo.png';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const ForgotPassword = (): React.ReactElement => {
  const [email, setEmail] = useState('');
  const [emailError, setEmailError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const normalizedEmail = email.trim();

    if (!normalizedEmail) {
      setEmailError('Email is required.');
      return;
    }

    if (!EMAIL_PATTERN.test(normalizedEmail)) {
      setEmailError('Enter a valid email address.');
      return;
    }

    setEmailError('');
    setIsLoading(true);
    try {
      await forgotPassword({ email: normalizedEmail });
      setEmail(normalizedEmail);
      setIsSuccess(true);
    } catch (error: unknown) {
      const apiError = parseApiError(error, 'Unable to send the reset link. Please try again.');
      if (apiError.fieldErrors.email) {
        setEmailError(apiError.fieldErrors.email);
      } else {
        toast.error(apiError.message);
      }
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
            <Link to="/" className="mb-5 inline-flex items-center gap-2.5 no-underline">
              <img src={logo} alt="EHub" className="w-[38px] h-[38px] object-contain" />
              <span className="text-[20px] font-extrabold tracking-tight">
                <span className="text-[#F08A5D]">E</span>
                <span className="text-[#1E5E9F]">HUB</span>
              </span>
            </Link>
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
            <form onSubmit={handleSubmit} className="space-y-5" noValidate>
              <div>
                <Input
                  id="forgot-email"
                  type="email"
                  label="Email Address"
                  icon={Mail}
                  autoComplete="email"
                  required
                  value={email}
                  error={emailError}
                  aria-invalid={Boolean(emailError)}
                  onChange={(e) => {
                    setEmail(e.target.value);
                    if (emailError) setEmailError('');
                  }}
                />
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
