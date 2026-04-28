import React, { useState, useCallback, useEffect, useRef } from 'react';
import ClockControls from './components/ClockControls.jsx';
import AttendanceTable from './components/AttendanceTable.jsx';
import * as api from './services/api.js';

export default function App() {
  const [employeeId, setEmployeeId]   = useState('');
  const [records, setRecords]         = useState([]);
  const [loading, setLoading]         = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [status, setStatus]           = useState(null); // { type: 'success'|'error', message }

  // Auto-dismiss the status banner after 5 s
  const dismissTimer = useRef(null);
  const showStatus = useCallback((type, message) => {
    setStatus({ type, message });
    clearTimeout(dismissTimer.current);
    dismissTimer.current = setTimeout(() => setStatus(null), 5000);
  }, []);

  const loadHistory = useCallback(async (id) => {
    if (!id?.trim()) return;
    setHistoryLoading(true);
    try {
      const data = await api.fetchHistory(id.trim());
      setRecords(data);
    } catch {
      // Non-critical – the table just stays empty
    } finally {
      setHistoryLoading(false);
    }
  }, []);

  // Reload history whenever the employee ID changes (debounced 600 ms)
  useEffect(() => {
    const timer = setTimeout(() => loadHistory(employeeId), 600);
    return () => clearTimeout(timer);
  }, [employeeId, loadHistory]);

  const handleClockIn = async () => {
    setLoading(true);
    try {
      await api.clockIn(employeeId.trim());
      showStatus('success', 'Clocked in successfully.');
      await loadHistory(employeeId);
    } catch (err) {
      showStatus('error', err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleClockOut = async () => {
    setLoading(true);
    try {
      await api.clockOut(employeeId.trim());
      showStatus('success', 'Clocked out successfully.');
      await loadHistory(employeeId);
    } catch (err) {
      showStatus('error', err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-indigo-50 via-white to-sky-50 px-4 py-10">
      <div className="mx-auto max-w-3xl flex flex-col gap-8">

        {/* Header */}
        <header className="text-center">
          <h1 className="text-3xl font-bold text-indigo-700 tracking-tight">WorkClock</h1>
          <p className="mt-1 text-sm text-gray-500">Employee Attendance Tracker</p>
        </header>

        {/* Clock controls card */}
        <ClockControls
          employeeId={employeeId}
          onEmployeeIdChange={setEmployeeId}
          onClockIn={handleClockIn}
          onClockOut={handleClockOut}
          loading={loading}
          status={status}
        />

        {/* History table */}
        <AttendanceTable records={records} loading={historyLoading} />

        {/* Footer note */}
        <p className="text-center text-xs text-gray-400">
          All times are displayed in your local timezone. Stored in UTC on the server.
        </p>
      </div>
    </div>
  );
}
