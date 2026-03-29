import { useState, useCallback, useRef, useEffect } from 'react'; 
import FullCalendar from '@fullcalendar/react';
import dayGridPlugin from '@fullcalendar/daygrid';
import interactionPlugin from '@fullcalendar/interaction';
import type { DatesSetArg, EventInput, EventHoveringArg, EventClickArg } from '@fullcalendar/core';
import { jwtDecode } from 'jwt-decode'; 
import api from '../services/api';
import ReservationModal, { type ReservationData } from '../components/ReservationModal'; 
import { getToken, clearToken } from '../services/authStorage';
import './CalendarPage.css';

interface TokenPayload {
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"?: string;
  nameid?: string;
  sub?: string; 
  id?: string;  
  isAdmin?: string; // <-- This is the claim we added (will be "True" or "False")
}

interface Reservation {
  id: number;
  startTime: string;
  endTime: string;
  // isAllDay: boolean; <-- REMOVED PROPERTY
  airplane: { name: string };
  airplaneId: number;
  personnelId: number;
  personnel: { fullName: string };
  location: string;
  flightDetails: string;
  passengerCount: number;
  notes: string;
  createdByUserId: string; 
}

interface TooltipData {
  x: number;
  y: number;
  title: string;
  location: string;
  flightDetails: string;
  passengerCount: number;
  notes: string;
  startDate: string;
  endDate: string;
}

const parseDateOnly = (dateStr: string) => {
  const [year, month, day] = dateStr.split('-').map(Number);
  return new Date(year, month - 1, day);
};

const formatDateOnly = (date: Date) => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

const addDays = (dateStr: string, days: number) => {
  const date = parseDateOnly(dateStr);
  date.setDate(date.getDate() + days);
  return formatDateOnly(date);
};

const toDateOnly = (dateTime: string) => dateTime ? dateTime.substring(0, 10) : "";

export default function CalendarPage() {
  const getInitialDate = () => {
    const saved = sessionStorage.getItem('petro_calendar_date');
    return saved ? new Date(saved) : new Date();
  };

  const [initialDate] = useState(getInitialDate());
  const [events, setEvents] = useState<EventInput[]>([]);
  const [tooltip, setTooltip] = useState<TooltipData | null>(null);
  
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [modalData, setModalData] = useState<ReservationData | null>(null);
  const [isReadOnlyMode, setIsReadOnlyMode] = useState(false); 
  const [currentUserId, setCurrentUserId] = useState<string>("");
  const [isAdmin, setIsAdmin] = useState(false); // <--- ADMIN STATE

  const [currentMonth, setCurrentMonth] = useState(getInitialDate().getMonth());
  const [currentYear, setCurrentYear] = useState(getInitialDate().getFullYear());

  const calendarRef = useRef<FullCalendar>(null);

  useEffect(() => {
    const token = getToken();
    if (token) {
        try {
            const decoded = jwtDecode<TokenPayload>(token);
            const id = decoded["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] 
                       || decoded.nameid
                       || decoded.sub 
                       || decoded.id
                       || "";

            // --- THE CRITICAL FIX FOR ADMIN CHECK ---
            const adminClaimValue = decoded.isAdmin ? decoded.isAdmin.toLowerCase() : '';
            const adminCheck = adminClaimValue === "true";
            
            setTimeout(() => {
                setCurrentUserId(id);
                setIsAdmin(adminCheck);
            }, 0);
        } catch (e) {
            console.error("Invalid token", e);
        }
    }
  }, []);

  const fetchReservations = useCallback(async (dateInfo: DatesSetArg) => {
    try {
      const centerDate = dateInfo.view.calendar.getDate();
      sessionStorage.setItem('petro_calendar_date', centerDate.toISOString());
      setCurrentMonth(centerDate.getMonth());
      setCurrentYear(centerDate.getFullYear());

      const response = await api.get<Reservation[]>('/Reservations', {
        params: { start: dateInfo.startStr, end: dateInfo.endStr }
      });

      const calendarEvents = response.data.map((res) => {
          const startDate = toDateOnly(res.startTime);
          const endDate = toDateOnly(res.endTime || res.startTime);
          const endExclusive = addDays(endDate, 1);

          return {
            id: res.id.toString(),
            title: `${res.airplane.name} - ${res.personnel.fullName}`,
            start: startDate,
            end: endExclusive,
            allDay: true,
            backgroundColor: '#5cb85c',
            borderColor: '#4cae4c',
            extendedProps: { 
                location: res.location,
                flightDetails: res.flightDetails,
                passengerCount: res.passengerCount,
                notes: res.notes,
                airplaneId: res.airplaneId,
                personnelId: res.personnelId,
                createdByUserId: res.createdByUserId 
            }
          }
      });

      setEvents(calendarEvents);
    } catch (error) {
      console.error("Failed to load reservations", error);
    }
  }, []);

  const handleEventClick = (info: EventClickArg) => {
    setTooltip(null); 
    const props = info.event.extendedProps;
    
    // Allow Admins to Edit ANYTHING, otherwise check Owner
    const isOwner = props.createdByUserId && currentUserId && 
                    props.createdByUserId.toLowerCase() === currentUserId.toLowerCase();
    
    // If you are owner OR you are Admin, you can edit
    setIsReadOnlyMode(!(isOwner || isAdmin)); 

    const endInclusive = info.event.endStr ? addDays(info.event.endStr, -1) : info.event.startStr;
    const data: ReservationData = {
        id: Number(info.event.id),
        startTime: info.event.startStr,
        endTime: endInclusive,
        // isAllDay: info.event.allDay, <-- REMOVED PROPERTY
        airplaneId: props.airplaneId,
        personnelId: props.personnelId,
        location: props.location,
        flightDetails: props.flightDetails,
        passengerCount: props.passengerCount,
        notes: props.notes
    };
    setModalData(data); 
    setIsModalOpen(true); 
  };

  const openNewModal = () => {
      setModalData(null); 
      setIsReadOnlyMode(false); 
      setIsModalOpen(true);
  }

  const handleMouseEnter = (info: EventHoveringArg) => {
    const rect = info.el.getBoundingClientRect();
    const props = info.event.extendedProps;
    const endInclusive = info.event.endStr ? addDays(info.event.endStr, -1) : info.event.startStr;
    setTooltip({
        x: rect.left + rect.width / 2,
        y: rect.top - 10,
        title: info.event.title,
        startDate: info.event.startStr,
        endDate: endInclusive,
        location: props.location,
        flightDetails: props.flightDetails,
        passengerCount: props.passengerCount,
        notes: props.notes
    });
  };

  const handleMouseLeave = () => setTooltip(null);
  
  const handleLogout = () => { 
      clearToken(); 
      sessionStorage.removeItem('petro_calendar_date'); 
      window.location.href = '/login'; 
  };
  
  const refresh = () => window.location.reload();
  const goToday = () => calendarRef.current?.getApi().today();
  const handleMonthChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newMonth = parseInt(e.target.value);
    setCurrentMonth(newMonth);
    calendarRef.current?.getApi().gotoDate(new Date(currentYear, newMonth, 1));
  };
  const handleYearChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newYear = parseInt(e.target.value);
    setCurrentYear(newYear);
    calendarRef.current?.getApi().gotoDate(new Date(newYear, currentMonth, 1));
  };

  return (
    <div className="petro-container">
        <video
            autoPlay
            loop
            muted
            playsInline
            className="calendar-background-video"
        >
            <source src="/assets/birdvid.mp4" type="video/mp4" />
            Your browser does not support the video tag.
        </video>
        <div className="calendar-video-overlay"></div>
        <div className="petro-header">
            <span className="petro-title">PetroTransit</span>
            <button className="petro-btn petro-btn-primary" onClick={openNewModal} style={{marginRight: '15px'}}>
                + New Reservation
            </button>
            <button className="petro-btn" onClick={refresh}>↻ Refresh</button>
            <button className="petro-btn" onClick={goToday}>Today</button>

            <div style={{marginLeft: '20px', display:'flex', gap:'5px'}}>
                <button className="petro-btn" onClick={() => calendarRef.current?.getApi().prev()}>◀</button>
                <button className="petro-btn" onClick={() => calendarRef.current?.getApi().next()}>▶</button>
            </div>

            <div style={{marginLeft: '10px', display:'flex', gap:'5px', alignItems:'center'}}>
                <select className="petro-select" value={currentMonth} onChange={handleMonthChange}>
                    {Array.from({ length: 12 }, (_, i) => (
                        <option key={i} value={i}>{new Date(0, i).toLocaleString('default', { month: 'long' })}</option>
                    ))}
                </select>
                <select className="petro-select" value={currentYear} onChange={handleYearChange}>
                    {[2024, 2025, 2026, 2027, 2028, 2029, 2030].map(year => (
                        <option key={year} value={year}>{year}</option>
                    ))}
                </select>
            </div>
            
            <div style={{marginLeft: 'auto', display: 'flex', gap: '10px'}}>
                {/* ADMIN BUTTON DISPLAY LOGIC */}
                {isAdmin && (
                    <button 
                        className="petro-btn" 
                        style={{backgroundColor: '#28a745', color: 'white'}}
                        onClick={() => window.location.href = '/admin'}
                    >
                        Admin
                    </button>
                )}
                <button className="petro-btn petro-btn-primary" onClick={handleLogout}>Logout</button>
            </div>
        </div>

        <div className="calendar-wrapper">
            <div className="calendar-card">
                <FullCalendar
                    ref={calendarRef} 
                    plugins={[dayGridPlugin, interactionPlugin]}
                    initialView="dayGridMonth"
                    initialDate={initialDate}
                    headerToolbar={false}
                    events={events}
                    datesSet={fetchReservations}
                    height="100%"
                    eventDisplay="block"
                    displayEventTime={false}
                    eventMouseEnter={handleMouseEnter}
                    eventMouseLeave={handleMouseLeave}
                    eventClick={handleEventClick} 
                />
            </div>
        </div>

        {tooltip && (
            <div className="petro-tooltip" style={{ top: tooltip.y, left: tooltip.x }}>
                <div className="tooltip-header">{tooltip.title}</div>
                <div className="tooltip-body">
                    <p><strong>Loc:</strong> {tooltip.location}</p>
                    <p><strong>Date:</strong> {tooltip.startDate}{tooltip.endDate !== tooltip.startDate ? ` - ${tooltip.endDate}` : ''}</p>
                    <p><strong>Pax:</strong> {tooltip.passengerCount}</p>
                    {tooltip.flightDetails && <p><strong>Details:</strong> {tooltip.flightDetails}</p>}
                    {tooltip.notes && <p className="tooltip-notes">"{tooltip.notes}"</p>}
                </div>
            </div>
        )}

        <ReservationModal 
            isOpen={isModalOpen} 
            onClose={() => setIsModalOpen(false)} 
            onSuccess={() => window.location.reload()} 
            initialData={modalData} 
            isReadOnly={isReadOnlyMode} 
        />
    </div>
  );
}
