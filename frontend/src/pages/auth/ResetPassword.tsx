import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useParams, useNavigate, useSearchParams } from 'react-router-dom';
import { Key, CheckCircle2, ArrowRight, AlertCircle } from 'lucide-react';
import Button from '../../components/ui/Button';
import Input from '../../components/ui/Input';
import { resetPassword } from '../../api/authApi';
import { AUTH_ERROR_CODES } from '../../types/auth';
import { parseApiError } from '../../utils/apiError';
import toast from 'react-hot-toast';
import logo from '../../assets/logo.png';

interface ResetPasswordErrors {
  password?: string;
  confirmPassword?: string;
}

const ResetPassword = (): React.ReactElement => {
  const { token: pathToken } = useParams<{ token?: string }>();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token')?.trim() || pathToken?.trim() || '';
  const navigate = useNavigate();
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [errors, setErrors] = useState<ResetPasswordErrors>({});
  const [formError, setFormError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);

  useEffect(() => {
    if (!isSuccess) return undefined;

    const redirectTimer = window.setTimeout(() => navigate('/login'), 3000);
    return () => window.clearTimeout(redirectTimer);
  }, [isSuccess, navigate]);

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const nextErrors: ResetPasswordErrors = {};

    if (!password) nextErrors.password = 'New password is required.';
    else if (password.length < 8) nextErrors.password = 'Password must be at least 8 characters.';

    if (!confirmPassword) nextErrors.confirmPassword = 'Please confirm your new password.';
    else if (password !== confirmPassword) nextErrors.confirmPassword = 'Passwords do not match.';

    setErrors(nextErrors);
    setFormError('');
    if (Object.keys(nextErrors).length > 0 || !token) return;

    setIsLoading(true);
    try {
      await resetPassword({ token, newPassword: password, confirmPassword });
      setIsSuccess(true);
      toast.success('Password reset successfully');
    } catch (error: unknown) {
      const apiError = parseApiError(error, 'Unable to reset your password. Please try again.');
      setErrors({
        password: apiError.fieldErrors.newPassword,
        confirmPassword: apiError.fieldErrors.confirmPassword,
      });

      if (apiError.code === AUTH_ERROR_CODES.PASSWORD_RESET_TOKEN_INVALID) {
        setFormError('This password reset link is invalid or has expired. Request a new link.');
      } else if (!apiError.fieldErrors.newPassword && !apiError.fieldErrors.confirmPassword) {
        setFormError(apiError.message);
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-hero flex items-center justify-center p-6 relative overflow-hidden">
      {/* Decorative blobs */}
      <div className="absolute top-[-15%] left-[-10%] w-[500px] h-[500px] bg-primary-100/40 rounded-full blur-[120px]" />
      <div className="absolute bottom-[-10%] right-[-5%] w-[400px] h-[400px] bg-secondary-100/30 rounded-full blur-[100px]" />
      <div className="absolute top-[30%] right-[15%] w-[250px] h-[250px] bg-cyan-100/40 rounded-full blur-[80px]" />

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
            <h1 className="text-heading font-bold text-slate-900">Create New Password</h1>
            <p className="text-body text-slate-500 mt-1">Your new password must be different from previously used passwords.</p>
          </div>

          {!token ? (
            <div className="flex flex-col items-center text-center space-y-4" role="alert">
              <div className="w-16 h-16 bg-danger/10 rounded-2xl flex items-center justify-center">
                <AlertCircle className="w-8 h-8 text-danger" />
              </div>
              <h3 className="text-xl font-bold text-slate-900">Invalid reset link</h3>
              <p className="text-slate-500 text-body leading-relaxed">
                This link does not contain a reset token. Request a new password reset email.
              </p>
              <Link to="/forgot-password" className="w-full mt-4">
                <Button variant="gradient" className="w-full">Request New Link</Button>
              </Link>
            </div>
          ) : isSuccess ? (
            <div className="flex flex-col items-center text-center space-y-4">
              <div className="w-16 h-16 bg-success-50 rounded-2xl flex items-center justify-center">
                <CheckCircle2 className="w-8 h-8 text-success" />
              </div>
              <h3 className="text-xl font-bold text-slate-900">Password reset!</h3>
              <p className="text-slate-500 text-body leading-relaxed">
                Your password has been successfully reset. Redirecting to login...
              </p>
              <Link to="/login" className="w-full mt-4">
                <Button variant="gradient" className="w-full" iconRight={ArrowRight}>Go to Login</Button>
              </Link>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-5" noValidate>
              {formError && (
                <div className="flex gap-3 rounded-xl border border-danger/30 bg-danger/10 p-3.5 text-left" role="alert">
                  <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-danger" />
                  <div>
                    <p className="text-body-sm text-slate-700">{formError}</p>
                    {formError.includes('expired') && (
                      <Link to="/forgot-password" className="mt-1 inline-block text-body-sm font-medium text-primary hover:underline">
                        Request a new reset link
                      </Link>
                    )}
                  </div>
                </div>
              )}

              <div>
                <Input
                  id="new-password"
                  type="password"
                  label="New Password"
                  icon={Key}
                  autoComplete="new-password"
                  required
                  minLength={8}
                  value={password}
                  error={errors.password}
                  aria-invalid={Boolean(errors.password)}
                  onChange={(e) => {
                    setPassword(e.target.value);
                    setErrors((current) => ({ ...current, password: undefined }));
                    setFormError('');
                  }}
                  placeholder="••••••••"
                />
              </div>

              <div>
                <Input
                  id="confirm-password"
                  type="password"
                  label="Confirm Password"
                  icon={Key}
                  autoComplete="new-password"
                  required
                  minLength={8}
                  value={confirmPassword}
                  error={errors.confirmPassword}
                  aria-invalid={Boolean(errors.confirmPassword)}
                  onChange={(e) => {
                    setConfirmPassword(e.target.value);
                    setErrors((current) => ({ ...current, confirmPassword: undefined }));
                    setFormError('');
                  }}
                  placeholder="••••••••"
                />
              </div>

              <Button type="submit" variant="gradient" className="w-full" size="lg" isLoading={isLoading}>
                Reset Password
              </Button>
            </form>
          )}
        </div>
      </div>
    </div>
  );
};

export default ResetPassword;
