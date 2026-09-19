import apiClient from './apiClient';

/**
 * Download appointment calendar invite (.ics) for the current user.
 * Backend: GET /Appointments/{id}/calendar.ics (text/calendar).
 *
 * @param {number|string} appointmentId
 * @param {{ filename?: string }} [options]
 */
export async function downloadAppointmentIcs(appointmentId, options = {}) {
  const response = await apiClient.get(`/Appointments/${appointmentId}/calendar.ics`, {
    responseType: 'blob',
    // Avoid forcing application/json Accept for calendar content
    headers: { Accept: 'text/calendar, text/plain, */*' },
  });

  const contentType = String(response.headers?.['content-type'] || '');
  // Some error payloads still arrive as blob with JSON type
  if (contentType.includes('application/json')) {
    const text = await response.data.text();
    let message = 'Could not download calendar file';
    try {
      const json = JSON.parse(text);
      message = json.message || json.errors?.join?.('. ') || message;
    } catch {
      // keep default
    }
    throw new Error(message);
  }

  const blob = response.data instanceof Blob
    ? response.data
    : new Blob([response.data], { type: 'text/calendar;charset=utf-8' });

  // Ensure .ics MIME for OS handlers
  const fileBlob = blob.type && blob.type.includes('calendar')
    ? blob
    : new Blob([blob], { type: 'text/calendar;charset=utf-8' });

  const filename = options.filename || `appointment-${appointmentId}.ics`;
  const url = URL.createObjectURL(fileBlob);
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.rel = 'noopener';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    // Delay revoke slightly so some mobile browsers finish the download
    setTimeout(() => URL.revokeObjectURL(url), 1500);
  }
}
