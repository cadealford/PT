import { useState, useEffect } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import api from '../services/api';
import { isAxiosError } from 'axios';
import './AuthUtilityPage.css'; // <-- NEW CSS IMPORT

export default function ResetPasswordPage() {
    const [searchParams] = useSearchParams();
    
    const [email, setEmail] = useState('');
    const [token, setToken] = useState('');
    
    const [inputEmail, setInputEmail] = useState(''); 
    const [newPassword, setNewPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');

    const [message, setMessage] = useState('');
    const [error, setError] = useState('');

    useEffect(() => {
        const emailParam = searchParams.get('email');
        const tokenParam = searchParams.get('token');

        if (emailParam && tokenParam) {
            const normalizedToken = tokenParam.replace(/ /g, '+');
            setEmail(emailParam);
            setToken(normalizedToken);
        }
    }, [searchParams]);

    const handleRequestSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setMessage('');
        setError('');

        try {
            await api.post('/Auth/forgot-password', { email: inputEmail });
            setMessage("If an account exists for that email, a password reset link has been sent to your inbox.");
        } catch (err: unknown) {
            console.error(err);
             if (isAxiosError(err) && err.response) {
                 setError(err.response.data?.title || "Request failed. Please try again.");
            } else {
                 setError("An unexpected error occurred.");
            }
        }
    };

    const handleResetSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setMessage('');
        setError('');
        
        if (newPassword !== confirmPassword) {
            setError("Passwords do not match.");
            return;
        }

        try {
            const response = await api.post('/Auth/reset-password', {
                email: email, 
                token: token, 
                newPassword: newPassword
            });
            
            if (response.status === 200) {
                setMessage("Your password has been reset successfully! You can now log in.");
                setNewPassword('');
                setConfirmPassword('');
                setToken(''); 
            }
        } catch (err: unknown) {
            console.error(err);
            if (isAxiosError(err) && err.response) {
                const errorData = err.response.data?.errors;
                if (errorData) {
                    const errorMessages = Object.values(errorData).flat().join(' ');
                    setError(`Password reset failed: ${errorMessages}`);
                } else {
                    setError(err.response?.data?.title || "Password reset failed. The link may be expired or invalid.");
                }
            } else {
                 setError("An unexpected error occurred.");
            }
        }
    };

    // --- RENDER LOGIC ---

    if (token && email) {
        return (
            <div className="auth-utility-container"> {/* <-- NEW CONTAINER CLASS */}
                <div className="auth-utility-card"> {/* <-- NEW CARD CLASS */}
                    <h2>Reset Password</h2>
                    <p>Enter your new password for **{email}**.</p>
                    
                    <form onSubmit={handleResetSubmit} className="auth-utility-form"> {/* <-- NEW FORM CLASS */}
                        {error && <div className="error-message">{error}</div>}
                        {message && <div className="success-message">{message}</div>}

                        <input
                            type="password"
                            placeholder="New Password"
                            value={newPassword}
                            onChange={(e) => setNewPassword(e.target.value)}
                            required
                        />
                        <input
                            type="password"
                            placeholder="Confirm New Password"
                            value={confirmPassword}
                            onChange={(e) => setConfirmPassword(e.target.value)}
                            required
                        />
                        <button type="submit" className="login-button">Reset Password</button>
                    </form>
                    <p className="return-link"><Link to="/login">Back to Login</Link></p>
                </div>
            </div>
        );
    }
    
    return (
        <div className="auth-utility-container"> {/* <-- NEW CONTAINER CLASS */}
            <div className="auth-utility-card"> {/* <-- NEW CARD CLASS */}
                <h2>Forgot Password</h2>
                <p>Enter the email address associated with your account to receive a reset link.</p>
                
                <form onSubmit={handleRequestSubmit} className="auth-utility-form"> {/* <-- NEW FORM CLASS */}
                    {error && <div className="error-message">{error}</div>}
                    {message && <div className="success-message">{message}</div>}

                    <input
                        type="email"
                        placeholder="Email Address"
                        value={inputEmail}
                        onChange={(e) => setInputEmail(e.target.value)}
                        required
                    />
                    <button type="submit" className="login-button">Send Reset Link</button>
                </form>
                <p className="return-link"><Link to="/login">Back to Login</Link></p>
            </div>
        </div>
    );
}
