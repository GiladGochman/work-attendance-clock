const BASE = '/api/attendance';

async function request(path, options = {}) {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });

  const body = await res.json().catch(() => null);

  if (!res.ok) {
    const message =
      body?.error ??
      `Request failed with status ${res.status}`;
    throw Object.assign(new Error(message), { status: res.status, body });
  }

  return body;
}

export const clockIn  = (employeeId) =>
  request('/clockin',  { method: 'POST', body: JSON.stringify({ employeeId }) });

export const clockOut = (employeeId) =>
  request('/clockout', { method: 'POST', body: JSON.stringify({ employeeId }) });

export const fetchHistory = (employeeId) =>
  request(`/history?employeeId=${encodeURIComponent(employeeId)}`);
