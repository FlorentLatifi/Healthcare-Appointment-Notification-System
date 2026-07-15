/**
 * Decide where to send the user after a successful login/session restore.
 * @param {{ role?: string, patientId?: number|null, doctorId?: number|null }} session
 * @returns {string} path
 */
export function postLoginPath(session) {
  const role = session?.role || session?.user?.role;
  const patientId = session?.patientId;
  const doctorId = session?.doctorId;
  const linked = (id) => id != null && Number(id) > 0;

  if (role === 'Admin') return '/admin';
  if (role === 'Doctor') return linked(doctorId) ? '/doctor-dashboard' : '/doctor-dashboard';
  if (role === 'Patient') return linked(patientId) ? '/dashboard' : '/create-patient';
  return '/dashboard';
}
