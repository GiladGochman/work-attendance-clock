import React from 'react';

function formatZurich(dateString) {
  if (!dateString) return '—';
  // The API returns Zurich local time tagged with 'Z' to prevent browser offset math.
  // Display with timeZone:'UTC' so the value is shown as-is (no conversion applied).
  return new Date(dateString).toLocaleString('de-CH', {
    timeZone: 'UTC',
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

function formatDuration(minutes) {
  if (minutes == null) return <span className="text-amber-600 font-medium">Active</span>;
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

/**
 * @param {{ records: Array, loading: boolean }} props
 */
export default function AttendanceTable({ records, loading }) {
  return (
    <div className="bg-white rounded-2xl shadow-md overflow-hidden">
      <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
        <h2 className="text-base font-semibold text-gray-700">Attendance History</h2>
        {loading && (
          <span className="text-xs text-gray-400 animate-pulse">Loading…</span>
        )}
      </div>

      {records.length === 0 && !loading ? (
        <p className="px-6 py-8 text-center text-sm text-gray-400">
          No records found. Enter an Employee ID and clock in to get started.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-gray-50 text-gray-500 uppercase text-xs tracking-wide">
              <tr>
                <th className="px-6 py-3 text-left">Date</th>
                <th className="px-6 py-3 text-left">Clock In</th>
                <th className="px-6 py-3 text-left">Clock Out</th>
                <th className="px-6 py-3 text-left">Duration</th>
                <th className="px-6 py-3 text-left">Source IP</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {records.map((r) => {
                const clockInDate = r.clockInUtc
                  ? new Date(r.clockInUtc).toLocaleDateString('de-CH', { timeZone: 'UTC', dateStyle: 'medium' })
                  : '—';
                const clockInTime = r.clockInUtc
                  ? new Date(r.clockInUtc).toLocaleTimeString('de-CH', { timeZone: 'UTC', timeStyle: 'short' })
                  : '—';
                const clockOutTime = r.clockOutUtc
                  ? new Date(r.clockOutUtc).toLocaleTimeString('de-CH', { timeZone: 'UTC', timeStyle: 'short' })
                  : null;

                return (
                  <tr key={r.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-6 py-3 font-medium text-gray-800">{clockInDate}</td>
                    <td className="px-6 py-3 text-gray-600">{clockInTime}</td>
                    <td className="px-6 py-3 text-gray-600">
                      {clockOutTime ?? (
                        <span className="inline-flex items-center gap-1 text-amber-600 font-medium">
                          <span className="inline-block h-2 w-2 rounded-full bg-amber-400 animate-pulse" />
                          Active
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-3">{formatDuration(r.durationMinutes)}</td>
                    <td className="px-6 py-3 text-gray-400 text-xs font-mono">{r.sourceIp ?? '—'}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
