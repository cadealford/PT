import { useState, useEffect } from 'react';
import api from '../Services/api';
import './AdminPage.css';
import { useNavigate, Link } from 'react-router-dom';
import { isAxiosError } from 'axios';

// --- Interface Definitions ---
interface User {
    id: string;
    userName: string;
    fullName: string;
    isAdmin: boolean;
}

interface Airplane {
    id: number;
    name: string;
    registrationNumber: string;
    capacity: number;
}

// Personnel interface updated: No 'department' field
interface Personnel {
    id: number;
    fullName: string;
}

// Interface for new user registration form
interface NewUser {
    username: string;
    password: string;
    fullName: string;
    email: string;
}

function getApiErrorMessage(err: unknown, fallback: string): string {
    if (!isAxiosError(err)) {
        return fallback;
    }

    const data = err.response?.data;

    if (Array.isArray(data)) {
        const descriptions = data
            .map(item => {
                if (typeof item === 'string') return item;
                if (item && typeof item === 'object' && 'description' in item && typeof item.description === 'string') {
                    return item.description;
                }
                return null;
            })
            .filter((message): message is string => Boolean(message));

        if (descriptions.length > 0) {
            return descriptions.join(' ');
        }
    }

    if (typeof data === 'string' && data.trim()) {
        return data;
    }

    if (data && typeof data === 'object' && 'title' in data && typeof data.title === 'string') {
        return data.title;
    }

    return fallback;
}

// --- Component Start ---
export default function AdminPage() {
    const [users, setUsers] = useState<User[]>([]);
    const [airplanes, setAirplanes] = useState<Airplane[]>([]);
    const [personnel, setPersonnel] = useState<Personnel[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const navigate = useNavigate();

    // State for managing Add forms
    const [newAirplane, setNewAirplane] = useState<Omit<Airplane, 'id'>>({ name: '', registrationNumber: '', capacity: 0 });
    // Personnel state updated: No 'department'
    const [newPersonnel, setNewPersonnel] = useState<Omit<Personnel, 'id'>>({ fullName: '' }); 
    const [newUser, setNewUser] = useState<NewUser>({ username: '', password: '', fullName: '', email: '' });

    // State for managing Edit forms
    const [editingAirplane, setEditingAirplane] = useState<Airplane | null>(null);
    const [editingPersonnel, setEditingPersonnel] = useState<Personnel | null>(null);

    // --- Data Fetching ---
    useEffect(() => {
        const checkAdminAndFetchData = async () => {
            try {
                const userResponse = await api.get<User[]>('/Auth/users');
                setUsers(userResponse.data);

                const airplaneResponse = await api.get<Airplane[]>('/Airplanes');
                setAirplanes(airplaneResponse.data);

                const personnelResponse = await api.get<Personnel[]>('/Personnel');
                setPersonnel(personnelResponse.data);

            } catch (err) {
                console.error("Admin data fetch failed:", err);
                if (isAxiosError(err) && err.response?.status === 403) {
                    alert("Access denied. Only administrators can view this page.");
                    navigate('/calendar');
                } else {
                    setError('Failed to fetch administration data. Please check the network.');
                }
            } finally {
                setLoading(false);
            }
        };

        checkAdminAndFetchData();
    }, [navigate]);

    if (loading) {
        return <div className="admin-loading">Loading Admin Panel...</div>;
    }

    if (error) {
        return <div className="admin-error">{error}</div>;
    }

    // --- Shared Refresh Function ---
    const refreshData = async () => {
        try {
            const [usersRes, planesRes, personnelRes] = await Promise.all([
                api.get<User[]>('/Auth/users'),
                api.get<Airplane[]>('/Airplanes'),
                api.get<Personnel[]>('/Personnel'),
            ]);
            setUsers(usersRes.data);
            setAirplanes(planesRes.data);
            setPersonnel(personnelRes.data);
            setError('');
        } catch (err) {
            console.error("Data refresh failed", err);
            setError('Data refresh failed after operation.');
        }
    };

    // --- ADD HANDLERS ---
    const handleAddAirplane = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await api.post('/Airplanes', newAirplane);
            setNewAirplane({ name: '', registrationNumber: '', capacity: 0 });
            refreshData();
        } catch (err) {
            console.error("Add airplane failed:", err);
            setError("Failed to add airplane. Check inputs.");
        }
    };

    const handleAddPersonnel = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await api.post('/Personnel', newPersonnel);
            // Reset state, no 'department' field
            setNewPersonnel({ fullName: '' }); 
            refreshData();
        } catch (err) {
            console.error("Add personnel failed:", err);
            setError("Failed to add personnel. Check inputs.");
        }
    };

    const handleAddUser = async (e: React.FormEvent) => { 
        e.preventDefault();
        try {
            await api.post('/Auth/register', newUser);
            setNewUser({ username: '', password: '', fullName: '', email: '' });
            refreshData();
        } catch (err) {
            console.error("Add user failed:", err);
            setError(getApiErrorMessage(err, "Failed to add user."));
        }
    };

    // --- DELETE HANDLERS ---
    const handleDelete = async (type: 'plane' | 'personnel' | 'user', id: string | number) => {
        if (!window.confirm(`Are you sure you want to delete this ${type} with ID ${id}?`)) {
            return;
        }

        try {
            let endpoint = '';
            switch (type) {
                case 'plane':
                    endpoint = `/Airplanes/${id}`;
                    break;
                case 'personnel':
                    endpoint = `/Personnel/${id}`;
                    break;
                case 'user':
                    endpoint = `/Auth/delete/${id}`;
                    break;
            }
            if (endpoint) {
                await api.delete(endpoint);
                refreshData();
            }
        } catch (err) {
            console.error(`Delete ${type} failed:`, err);
            if (isAxiosError(err) && err.response?.status === 400) {
                 setError(`Failed to delete ${type}. It may be referenced by an existing reservation.`);
            } else {
                 setError(`Failed to delete ${type}.`);
            }
        }
    };

    // --- EDIT HANDLERS (AIRPLANE & PERSONNEL) ---
    const handleEditSubmitAirplane = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!editingAirplane) return;
        try {
            await api.put(`/Airplanes/${editingAirplane.id}`, editingAirplane);
            setEditingAirplane(null);
            refreshData();
        } catch (err) {
            console.error("Update airplane failed:", err);
            setError("Failed to update airplane.");
        }
    };

    const handleEditSubmitPersonnel = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!editingPersonnel) return;
        try {
            // Updated payload, only contains fullName
            await api.put(`/Personnel/${editingPersonnel.id}`, { id: editingPersonnel.id, fullName: editingPersonnel.fullName });
            setEditingPersonnel(null);
            refreshData();
        } catch (err) {
            console.error("Update personnel failed:", err);
            setError("Failed to update personnel.");
        }
    };

    // --- USER MANAGEMENT HANDLERS ---
    const handlePromoteDemote = async (user: User) => {
        const action = user.isAdmin ? 'demote' : 'promote';
        if (!window.confirm(`Are you sure you want to ${action} ${user.userName}?`)) {
            return;
        }

        try {
            const endpoint = `/Auth/promote/${user.id}`; 
            await api.put(endpoint, null);
            refreshData();
        } catch (err) {
            console.error(`Promote/Demote failed:`, err);
            setError(`Failed to ${action} user.`);
        }
    };


    // --- RENDER FUNCTIONS ---
    const renderAirplaneEditForm = () => (
        <form onSubmit={handleEditSubmitAirplane} className="admin-edit-form">
            <h3>Edit Airplane (ID: {editingAirplane?.id})</h3>
            <input
                type="text"
                placeholder="Name"
                value={editingAirplane?.name || ''}
                onChange={e => setEditingAirplane(p => p ? { ...p, name: e.target.value } : null)}
                required
            />
            <input
                type="text"
                placeholder="Registration Number"
                value={editingAirplane?.registrationNumber || ''}
                onChange={e => setEditingAirplane(p => p ? { ...p, registrationNumber: e.target.value } : null)}
                required
            />
            <input
                type="number"
                placeholder="Capacity"
                value={editingAirplane?.capacity || 0}
                onChange={e => setEditingAirplane(p => p ? { ...p, capacity: parseInt(e.target.value) || 0 } : null)}
                required
            />
            <div className="admin-form-actions">
                <button type="submit" className="admin-btn admin-btn-save">Save Changes</button>
                <button type="button" className="admin-btn admin-btn-cancel" onClick={() => setEditingAirplane(null)}>Cancel</button>
            </div>
        </form>
    );

    const renderPersonnelEditForm = () => (
        <form onSubmit={handleEditSubmitPersonnel} className="admin-edit-form">
            <h3>Edit Personnel (ID: {editingPersonnel?.id})</h3>
            <input
                type="text"
                placeholder="Full Name"
                value={editingPersonnel?.fullName || ''}
                // Personnel state update simplified: No 'department' field
                onChange={e => setEditingPersonnel(p => p ? { ...p, fullName: e.target.value } : null)} 
                required
            />
            <div className="admin-form-actions">
                <button type="submit" className="admin-btn admin-btn-save">Save Changes</button>
                <button type="button" className="admin-btn admin-btn-cancel" onClick={() => setEditingPersonnel(null)}>Cancel</button>
            </div>
        </form>
    );
    
    // --- MAIN RETURN ---
    return (
        <div className="admin-page-container">
            <header className="admin-header">
                <h1>Administration Panel</h1>
                <Link to="/calendar" className="admin-back-link">← Back to Scheduler</Link>
            </header>
            
            <p className="admin-note">Manage users, aircraft, and flight personnel.</p>
            {error && <div className="admin-error-message">{error}</div>}

            {/* --- AIRPLANES MANAGEMENT --- */}
            <section className="admin-section">
                <h2>✈️ Aircraft Management</h2>
                {editingAirplane && renderAirplaneEditForm()}
                
                {!editingAirplane && (
                    <form onSubmit={handleAddAirplane} className="admin-add-form">
                        <input
                            type="text"
                            placeholder="Name (e.g., Falcon 900)"
                            value={newAirplane.name}
                            onChange={e => setNewAirplane({ ...newAirplane, name: e.target.value })}
                            required
                        />
                        <input
                            type="text"
                            placeholder="Registration (e.g., N123PT)"
                            value={newAirplane.registrationNumber}
                            onChange={e => setNewAirplane({ ...newAirplane, registrationNumber: e.target.value })}
                            required
                        />
                        <input
                            type="number"
                            placeholder="Capacity"
                            value={newAirplane.capacity || ''}
                            onChange={e => setNewAirplane({ ...newAirplane, capacity: parseInt(e.target.value) || 0 })}
                            required
                        />
                        <button type="submit" className="admin-btn admin-btn-add">Add Aircraft</button>
                    </form>
                )}
                
                <table className="admin-table">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Name</th>
                            <th>Registration</th>
                            <th>Capacity</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {airplanes.map(plane => (
                            <tr key={plane.id}>
                                <td>{plane.id}</td>
                                <td>{plane.name}</td>
                                <td>{plane.registrationNumber}</td>
                                <td>{plane.capacity}</td>
                                <td>
                                    <button 
                                        className="admin-btn admin-btn-edit" 
                                        onClick={() => { setEditingAirplane(plane); setEditingPersonnel(null); }}
                                    >
                                        Edit
                                    </button>
                                    <button 
                                        className="admin-btn admin-btn-delete" 
                                        onClick={() => handleDelete('plane', plane.id)}
                                    >
                                        Delete
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </section>

            {/* --- PERSONNEL MANAGEMENT --- */}
            <section className="admin-section">
                <h2>🧑‍✈️ Personnel Management</h2>
                {editingPersonnel && renderPersonnelEditForm()}

                {!editingPersonnel && (
                    <form onSubmit={handleAddPersonnel} className="admin-add-form">
                        <input
                            type="text"
                            placeholder="Full Name"
                            value={newPersonnel.fullName}
                            // Personnel input simplified: Only Full Name
                            onChange={e => setNewPersonnel({ ...newPersonnel, fullName: e.target.value })} 
                            required
                        />
                        <button type="submit" className="admin-btn admin-btn-add">Add Personnel</button>
                    </form>
                )}

                <table className="admin-table">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Name</th>
                            <th>Actions</th> {/* Department column removed */}
                        </tr>
                    </thead>
                    <tbody>
                        {personnel.map(p => (
                            <tr key={p.id}>
                                <td>{p.id}</td>
                                <td>{p.fullName}</td>
                                <td>
                                    <button 
                                        className="admin-btn admin-btn-edit" 
                                        onClick={() => { setEditingPersonnel(p); setEditingAirplane(null); }}
                                    >
                                        Edit
                                    </button>
                                    <button 
                                        className="admin-btn admin-btn-delete" 
                                        onClick={() => handleDelete('personnel', p.id)}
                                    >
                                        Delete
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </section>

            {/* --- USER MANAGEMENT --- */}
            <section className="admin-section">
                <h2>👥 User Management</h2>
                
                {/* NEW USER ADDITION FORM */}
                <form onSubmit={handleAddUser} className="admin-add-form user-add-form">
                    <input
                        type="text"
                        placeholder="Username"
                        value={newUser.username}
                        onChange={e => setNewUser({ ...newUser, username: e.target.value })}
                        required
                    />
                    <input
                        type="password"
                        placeholder="Password"
                        value={newUser.password}
                        onChange={e => setNewUser({ ...newUser, password: e.target.value })}
                        required
                    />
                    <input
                        type="text"
                        placeholder="Full Name"
                        value={newUser.fullName}
                        onChange={e => setNewUser({ ...newUser, fullName: e.target.value })}
                        required
                    />
                    <input
                        type="email"
                        placeholder="Email"
                        value={newUser.email}
                        onChange={e => setNewUser({ ...newUser, email: e.target.value })}
                        required
                    />
                    <button type="submit" className="admin-btn admin-btn-add">Add New User</button>
                </form>

                <table className="admin-table">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Username</th>
                            <th>Full Name</th>
                            <th>Role</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {users.map(user => (
                            <tr key={user.id}>
                                <td>{user.id.substring(0, 5)}...</td>
                                <td>{user.userName}</td>
                                <td>{user.fullName}</td>
                                <td>
                                    <span className={`admin-role ${user.isAdmin ? 'admin-role-admin' : 'admin-role-user'}`}>
                                        {user.isAdmin ? 'Administrator' : 'User'}
                                    </span>
                                </td>
                                <td>
                                    <button 
                                        className={`admin-btn admin-btn-${user.isAdmin ? 'demote' : 'promote'}`}
                                        onClick={() => handlePromoteDemote(user)}
                                    >
                                        {user.isAdmin ? 'Demote' : 'Promote'}
                                    </button>
                                    <button 
                                        className="admin-btn admin-btn-delete" 
                                        onClick={() => handleDelete('user', user.id)}
                                        disabled={user.isAdmin} 
                                    >
                                        Delete
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </section>
        </div>
    );
}
