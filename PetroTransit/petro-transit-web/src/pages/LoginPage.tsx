import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import api from '../services/api';
import { getToken, setToken } from '../services/authStorage';
import './LoginPage.css';

export default function LoginPage() {
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [rememberMe, setRememberMe] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        const token = getToken();
        if (token) {
            window.location.href = '/calendar';
        }
    }, []);

    const handleLogin = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            const response = await api.post('/Auth/login', { username, password });
            setToken(response.data.token, rememberMe);
            window.location.href = '/calendar';
        } catch (err) {
            console.error(err);
            setError('Invalid username or password');
        }
    };

    return (
        <div className="login-wrapper">
            {/* BACKGROUND VIDEO */}
            <video 
                autoPlay 
                loop 
                muted 
                playsInline 
                className="background-video"
            >
                {/* Local file path relative to the 'public' folder */}
                <source src="/assets/birdvid.mp4" type="video/mp4" />
                Your browser does not support the video tag.
            </video>

            {/* DARK OVERLAY */}
            <div className="video-overlay"></div>

            {/* LOGIN BOX */}
            <div className="login-box">
                <h2 className="login-title">PetroTransit</h2>
                <p className="login-subtitle">Flight Scheduler Login</p>
                
                <form onSubmit={handleLogin}>
                    {error && <div className="error-message">{error}</div>}
                    
                    <div className="input-group">
                        <input 
                            type="text" 
                            placeholder="Username" 
                            value={username} 
                            onChange={e => setUsername(e.target.value)} 
                            required 
                        />
                    </div>
                    
                    <div className="input-group">
                        <input 
                            type="password" 
                            placeholder="Password" 
                            value={password} 
                            onChange={e => setPassword(e.target.value)} 
                            required 
                        />
                    </div>

                    <label className="remember-row">
                        <input
                            type="checkbox"
                            checked={rememberMe}
                            onChange={e => setRememberMe(e.target.checked)}
                        />
                        Remember me
                    </label>

                    <button type="submit" className="login-btn">Sign In</button>
                </form>
                
                {/* --- FORGOT LINKS BLOCK --- */}
                <div className="forgot-links">
                    <Link to="/forgot-username" className="forgot-link">Forgot Username?</Link>
                    <span className="link-separator">|</span>
                    <Link to="/reset-password" className="forgot-link">Forgot Password?</Link>
                </div>
                {/* ------------------------------- */}
            </div>
        </div>
    );
}
