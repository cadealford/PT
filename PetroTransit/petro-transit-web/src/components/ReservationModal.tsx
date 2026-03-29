import { useState, useEffect } from 'react';
import { isAxiosError } from 'axios';
import api from '../services/api';
import './ReservationModal.css';

export interface ReservationData {
  id: number;
  startTime: string; 
  endTime: string;
  // isAllDay: boolean; <-- REMOVED
  airplaneId: number;
  personnelId: number;
  location: string;
  flightDetails: string;
  passengerCount: number;
  notes: string;
}

interface ReservationModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  initialData?: ReservationData | null;
  isReadOnly?: boolean;
}

interface Airplane { id: number; name: string; }
interface Personnel { id: number; fullName: string; }

// Define backend error shape
interface BackendError {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

// Helper to format Date for display in YYYY-MM-DD format
const formatForDisplay = (dateString: string | undefined): string => {
    if (!dateString) return '';
    return dateString.substring(0, 10);
};

const isValidDateOnly = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value);
const toUtcIso = (value: string, hour: number, minute: number, second: number) => {
    if (!isValidDateOnly(value)) return "";
    const [year, month, day] = value.split('-').map(Number);
    return new Date(Date.UTC(year, month - 1, day, hour, minute, second)).toISOString();
};
const toStartDateTime = (value: string) => toUtcIso(value, 0, 0, 0);
const toEndDateTime = (value: string) => toUtcIso(value, 23, 59, 59);

export default function ReservationModal({ isOpen, onClose, onSuccess, initialData, isReadOnly = false }: ReservationModalProps) {
  const [airplanes, setAirplanes] = useState<Airplane[]>([]);
  const [personnelList, setPersonnelList] = useState<Personnel[]>([]);

  // Form Fields (date-only)
  const [startTime, setStartTime] = useState('');
  const [endTime, setEndTime] = useState('');
  // const [isAllDay, setIsAllDay] = useState(false); <-- REMOVED STATE
  const [airplaneId, setAirplaneId] = useState('');
  const [personnelId, setPersonnelId] = useState('');
  const [location, setLocation] = useState('');
  const [flightDetails, setFlightDetails] = useState('');
  const [passengerCount, setPassengerCount] = useState(1);
  const [notes, setNotes] = useState('');
  
  const [error, setError] = useState('');

  useEffect(() => {
    if (isOpen) {
      setTimeout(() => {
        setError(''); 
      }, 0);
      
      api.get<Airplane[]>('/Airplanes').then(res => setAirplanes(res.data)).catch(console.error);
      api.get<Personnel[]>('/Personnel').then(res => setPersonnelList(res.data)).catch(console.error);

      setTimeout(() => {
          if (initialData) {
            
            setStartTime(formatForDisplay(initialData.startTime));
            setEndTime(formatForDisplay(initialData.endTime));
            
            // setIsAllDay(initialData.isAllDay); <-- REMOVED LOGIC
            setAirplaneId(initialData.airplaneId.toString());
            setPersonnelId(initialData.personnelId.toString());
            setLocation(initialData.location);
            setFlightDetails(initialData.flightDetails);
            setPassengerCount(initialData.passengerCount);
            setNotes(initialData.notes);
          } else {
            setStartTime(''); 
            setEndTime('');   
            // setIsAllDay(false); <-- REMOVED LOGIC
            setAirplaneId('');
            setPersonnelId('');
            setLocation('');
            setFlightDetails('');
            setPassengerCount(1);
            setNotes('');
          }
      }, 0);
    }
  }, [isOpen, initialData]);

  const handleClose = () => { setError(''); onClose(); };

  const handleDelete = async () => {
    if (!initialData || !window.confirm("Are you sure you want to delete this reservation?")) return;
    try {
      await api.delete(`/Reservations/${initialData.id}`);
      onSuccess();
      handleClose();
    } catch (err) {
      console.error(err);
      setError("Failed to delete reservation.");
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isReadOnly) return; 

    setError('');

    const finalStartTimeISO = toStartDateTime(startTime);
    const finalEndTimeISO = toEndDateTime(endTime);

    if (!finalStartTimeISO || !finalEndTimeISO) {
        setError("Invalid date. Please use YYYY-MM-DD.");
        return;
    }
    if (endTime < startTime) {
        setError("End date cannot be before start date.");
        return;
    }

    const payload = {
      ...(initialData ? { id: initialData.id } : {}),
      startTime: finalStartTimeISO,
      endTime: finalEndTimeISO,
      // isAllDay: false, <-- REMOVED FIELD
      airplaneId: Number(airplaneId),
      personnelId: Number(personnelId),
      location,
      flightDetails,
      passengerCount: Number(passengerCount),
      notes
    };

    try {
      if (initialData) {
        await api.put(`/Reservations/${initialData.id}`, payload);
      } else {
        await api.post('/Reservations', payload);
      }
      onSuccess(); 
      handleClose();  
    } catch (err) {
      if (isAxiosError(err) && err.response) {
        let msg = "Failed to save";
        if (typeof err.response.data === 'string') msg = err.response.data;
        else if (err.response.data && typeof err.response.data === 'object') {
              const backendError = err.response.data as BackendError;
              if (backendError.title) msg = backendError.title;
        }
        setError(msg);
      } else {
        console.error(err);
        setError("An unexpected error occurred.");
      }
    }
  };

  if (!isOpen) return null;

  let title = "New Reservation";
  if (initialData) title = isReadOnly ? "Reservation Details (View Only)" : "Edit Reservation";

  return (
    <div className="modal-overlay">
      <div className="modal-content">
        <div className="modal-header">
          <h3>{title}</h3>
          <button onClick={handleClose} className="close-btn">×</button>
        </div>
        
        <form onSubmit={handleSubmit} className="modal-form">
          {error && <div className="error-banner">{error}</div>}
          
          <fieldset disabled={isReadOnly} style={{border: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '12px'}}>
            <div className="form-row">
              <label>
                Airplane:
                <select value={airplaneId} onChange={e => setAirplaneId(e.target.value)} required>
                  <option value="">-- Select Airplane --</option>
                  {airplanes.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select>
              </label>
              <label>
                Reserved For:
                <select value={personnelId} onChange={e => setPersonnelId(e.target.value)} required>
                  <option value="">-- Select Person --</option>
                  {personnelList.map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}
                </select>
              </label>
            </div>

            <div className="form-row">
              <label>
                Start Date:
                <input 
                    type="date" 
                    value={startTime} 
                    onChange={e => setStartTime(e.target.value)} 
                    required 
                />
              </label>
              <label>
                End Date:
                <input 
                    type="date" 
                    value={endTime} 
                    onChange={e => setEndTime(e.target.value)} 
                    required 
                />
              </label>
            </div>

            {/* <div className="form-row checkbox-row">... REMOVED ALL DAY CHECKBOX ...</div> */}

            <label>
              Location:
              <input type="text" value={location} onChange={e => setLocation(e.target.value)} required placeholder="Destination" />
            </label>
            <label>
              Flight Details:
              <input type="text" value={flightDetails} onChange={e => setFlightDetails(e.target.value)} placeholder="Details..." />
            </label>
            <label>
              Passenger Count:
              <input type="number" value={passengerCount} onChange={e => setPassengerCount(Number(e.target.value))} min={0} />
            </label>
            <label>
              Notes:
              <textarea value={notes} onChange={e => setNotes(e.target.value)} rows={3}></textarea>
            </label>
          </fieldset>

          <div className="modal-actions" style={{justifyContent: 'space-between'}}>
            {initialData && !isReadOnly ? (
                <button type="button" onClick={handleDelete} className="btn-delete" style={{backgroundColor:'#d9534f', color:'white', border:'none', padding:'8px 16px', borderRadius:'4px', cursor:'pointer'}}>
                    Delete
                </button>
            ) : (
                <div></div> 
            )}

            <div style={{display:'flex', gap:'10px'}}>
              <button type="button" onClick={handleClose} className="btn-cancel">
                  {isReadOnly ? "Close" : "Cancel"}
              </button>
              {!isReadOnly && (
                  <button type="submit" className="btn-save">{initialData ? "Update" : "Save"}</button>
              )}
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
