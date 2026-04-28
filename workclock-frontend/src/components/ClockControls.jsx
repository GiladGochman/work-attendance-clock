import React from 'react';

function Spinner() {
  return (
    <svg
      className="animate-spin h-5 w-5 text-white"
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
    </svg>
  );
}

/**
 * @param {{ employeeId: string, onEmployeeIdChange: (v: string) => void,
 *           onClockIn: () => void, onClockOut: () => void,
 *           loading: boolean, status: {type: 'success'|'error', message: string} | null }} props
 */
export default function ClockControls({
  employeeId,
  onEmployeeIdChange,
  onClockIn,
  onClockOut,
  loading,
  status,
}) {
  return (
    <div className="bg-white rounded-2xl shadow-md p-8 flex flex-col gap-6">
      {/* Employee ID field */}
      <div>
        <label
          htmlFor="employeeId"
          className="block text-sm font-medium text-gray-700 mb-1"
        >
          Employee ID
        </label>
        <input
          id="employeeId"
          type="text"
          value={employeeId}
          onChange={(e) => onEmployeeIdChange(e.target.value)}
          placeholder="e.g. EMP001"
          disabled={loading}
          className="w-full rounded-lg border border-gray-300 px-4 py-2 text-sm
                     focus:outline-none focus:ring-2 focus:ring-indigo-400
                     disabled:bg-gray-100 disabled:cursor-not-allowed"
        />
      </div>

      {/* Action buttons */}
      <div className="flex gap-4">
        <button
          onClick={onClockIn}
          disabled={loading || !employeeId.trim()}
          className="flex-1 flex items-center justify-center gap-2 rounded-xl
                     bg-indigo-600 hover:bg-indigo-700 active:bg-indigo-800
                     text-white font-semibold py-3 transition
                     disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {loading ? <Spinner /> : '🟢'}
          Clock In
        </button>

        <button
          onClick={onClockOut}
          disabled={loading || !employeeId.trim()}
          className="flex-1 flex items-center justify-center gap-2 rounded-xl
                     bg-rose-500 hover:bg-rose-600 active:bg-rose-700
                     text-white font-semibold py-3 transition
                     disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {loading ? <Spinner /> : '🔴'}
          Clock Out
        </button>
      </div>

      {/* Status banner */}
      {status && (
        <div
          className={`rounded-lg px-4 py-3 text-sm font-medium ${
            status.type === 'success'
              ? 'bg-green-50 text-green-800 border border-green-200'
              : 'bg-red-50 text-red-800 border border-red-200'
          }`}
        >
          {status.message}
        </div>
      )}

      {/* Latency note shown while waiting for the external time API */}
      {loading && (
        <p className="text-center text-xs text-gray-400 animate-pulse">
          Fetching authoritative time from external API…
        </p>
      )}
    </div>
  );
}
