import { useState } from 'react';
import { Link } from 'react-router-dom';
import api from '../Services/api';
import { isAxiosError } from 'axios';
import './AuthUtilityPage.css'; // <-- NEW CSS IMPORT

export default function ForgotUsernamePage() {
    const [email, setEmail] = useState('');
    const [message, setMessage] = useState('');
    const [error, setError] = useState('');

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setMessage('');
        setError('');

        try {
            await api.post('/Auth/forgot-username', { email });
            setMessage("If an account exists for that email, your username has been sent to your inbox.");
        } catch (err: unknown) {
            console.error(err);
            if (isAxiosError(err) && err.response) {
                 setError(err.response.data?.title || "Request failed. Please try again.");
            } else {
                 setError("An unexpected error occurred.");
            }
        }
    };

    return (
        <div className="auth-utility-container"> {/* <-- NEW CONTAINER CLASS */}
            <div className="auth-utility-card"> {/* <-- NEW CARD CLASS */}
                <h2>Forgot Username</h2>
                <p>Enter the email address associated with your account.</p>
                
                <form onSubmit={handleSubmit} className="auth-utility-form"> {/* <-- NEW FORM CLASS */}
                    {error && <div className="error-message">{error}</div>}
                    {message && <div className="success-message">{message}</div>}

                    <input
                        type="email"
                        placeholder="Email Address"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        required
                        className="input-field"
                    />
                    <button type="submit" className="login-button">Send Username</button>
                </form>

                <p className="return-link"><Link to="/login">Back to Login</Link></p>
            </div>
        </div>
    );
}
