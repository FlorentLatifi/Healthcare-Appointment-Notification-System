/**
 * In-memory API mock for Playwright.
 * Intercepts /api/v1/** so happy-path UI flows run without SQL, Redis, or Stripe.
 */

const ok = (data, message = 'OK') => ({
  success: true,
  message,
  data,
  errors: null,
});

const fail = (message, status = 400, errors = null) => ({
  success: false,
  message,
  data: null,
  errors,
  status,
});

const weeklySchedule = [
  { dayOfWeek: 0, isWorkingDay: false, startTime: null, endTime: null },
  { dayOfWeek: 1, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 2, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 3, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 4, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 5, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 6, isWorkingDay: false, startTime: null, endTime: null },
];

const DOCTOR = {
  id: 5,
  firstName: 'Elena',
  lastName: 'Rivera',
  fullName: 'Elena Rivera',
  email: 'dr.rivera@clinic.test',
  phoneNumber: '+15551234001',
  licenseNumber: 'MED-E2E-5',
  specialties: ['Cardiology'],
  consultationFeeAmount: 80,
  consultationFeeCurrency: 'USD',
  isAcceptingPatients: true,
  isActive: true,
  yearsOfExperience: 12,
  weeklySchedule,
};

function json(route, body, status = 200) {
  return route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

/**
 * @param {import('@playwright/test').Page} page
 * @param {{ mode?: 'patient'|'doctor' }} [opts]
 */
export async function installApiMock(page, opts = {}) {
  const state = {
    mode: opts.mode || 'patient',
    users: new Map(),
    session: null, // { token, username, role, patientId, doctorId }
    patient: null,
    appointments: [],
    nextApptId: 100,
    paid: new Set(),
  };

  // Seed known doctor login for doctor flow
  state.users.set('e2e_doctor', {
    username: 'e2e_doctor',
    password: 'SecurePass123!',
    email: 'e2e.doctor@test.com',
    role: 'Doctor',
    doctorId: 5,
    patientId: null,
  });

  await page.route('**/api/v1/**', async (route) => {
    const req = route.request();
    const method = req.method().toUpperCase();
    const url = new URL(req.url());
    // pathname like /api/v1/Auth/login
    const path = url.pathname.replace(/^\/api\/v\d+\/?/i, '/').replace(/\/+$/, '') || '/';
    let body = null;
    if (method !== 'GET' && method !== 'HEAD') {
      try {
        body = req.postDataJSON();
      } catch {
        body = null;
      }
    }

    // ── Auth ──────────────────────────────────────────────
    if (method === 'POST' && path === '/Auth/refresh') {
      if (!state.session) {
        return json(route, fail('No refresh session', 401), 401);
      }
      return json(route, ok({
        token: state.session.token,
        username: state.session.username,
        role: state.session.role,
        patientId: state.session.patientId,
        doctorId: state.session.doctorId,
      }));
    }

    if (method === 'POST' && path === '/Auth/register') {
      const username = body?.username;
      if (!username || state.users.has(username)) {
        return json(route, fail('Username already exists', 400, { username: ['Username already exists'] }), 400);
      }
      state.users.set(username, {
        username,
        password: body.password,
        email: body.email,
        role: body.role === 'Doctor' ? 'Doctor' : 'Patient',
        patientId: null,
        doctorId: null,
      });
      return json(route, ok({ username }, 'Registered'), 201);
    }

    if (method === 'POST' && path === '/Auth/login') {
      const user = state.users.get(body?.username);
      if (!user || user.password !== body?.password) {
        return json(route, fail('Invalid username or password', 400), 400);
      }
      state.session = {
        token: `e2e-token-${user.username}`,
        username: user.username,
        role: user.role,
        patientId: user.patientId,
        doctorId: user.doctorId,
      };
      return json(route, ok({
        token: state.session.token,
        username: state.session.username,
        role: state.session.role,
        patientId: state.session.patientId,
        doctorId: state.session.doctorId,
      }));
    }

    if (method === 'POST' && path === '/Auth/logout') {
      state.session = null;
      return json(route, ok(null, 'Logged out'));
    }

    if (method === 'GET' && path === '/Auth/me') {
      if (!state.session) return json(route, fail('Unauthorized', 401), 401);
      return json(route, ok({
        username: state.session.username,
        role: state.session.role,
        patientId: state.session.patientId,
        doctorId: state.session.doctorId,
      }));
    }

    // ── Patients ──────────────────────────────────────────
    if (method === 'POST' && path === '/Patients') {
      if (!state.session || state.session.role !== 'Patient') {
        return json(route, fail('Forbidden', 403), 403);
      }
      const patientId = 42;
      state.patient = { id: patientId, ...body };
      const user = state.users.get(state.session.username);
      if (user) user.patientId = patientId;
      state.session.patientId = patientId;
      state.session.token = `e2e-token-${state.session.username}-p${patientId}`;
      return json(route, ok({
        id: patientId,
        token: state.session.token,
        username: state.session.username,
        role: 'Patient',
        patientId,
        doctorId: null,
      }), 201);
    }

    // ── Doctors catalog / schedule / availability ─────────
    if (method === 'GET' && (path === '/Doctors/accepting-patients' || path === '/Doctors')) {
      return json(route, ok({
        items: [DOCTOR],
        pageNumber: 1,
        pageSize: 100,
        totalCount: 1,
        totalPages: 1,
      }));
    }

    if (method === 'GET' && path === `/Doctors/${DOCTOR.id}`) {
      return json(route, ok(DOCTOR));
    }

    if (method === 'GET' && path === `/Doctors/${DOCTOR.id}/schedule`) {
      return json(route, ok({
        doctorId: DOCTOR.id,
        isActive: true,
        isAcceptingPatients: true,
        weeklySchedule,
      }));
    }

    if (method === 'GET' && path === `/Doctors/${DOCTOR.id}/availability`) {
      return json(route, ok({
        doctorId: DOCTOR.id,
        date: url.searchParams.get('date'),
        bookedSlots: [],
      }));
    }

    // ── Appointments ──────────────────────────────────────
    if (method === 'POST' && path === '/Appointments') {
      if (!state.session?.patientId) {
        return json(route, fail('Patient profile required', 400), 400);
      }
      const id = state.nextApptId++;
      const appt = {
        id,
        status: 'Pending',
        referenceCode: `REF-E2E-${id}`,
        patientId: state.session.patientId,
        doctorId: Number(body.doctorId) || DOCTOR.id,
        scheduledTime: body.scheduledTime,
        scheduledDate: body.scheduledTime ? String(body.scheduledTime).slice(0, 10) : '2099-06-15',
        scheduledTimeFormatted: '10:00 AM',
        reason: body.reason,
        appointmentType: body.appointmentType || 'Standard',
        consultationFeeAmount: DOCTOR.consultationFeeAmount,
        consultationFeeCurrency: DOCTOR.consultationFeeCurrency,
        doctor: { id: DOCTOR.id, fullName: DOCTOR.fullName },
        patient: {
          fullName: state.patient
            ? `${state.patient.firstName} ${state.patient.lastName}`
            : 'E2E Patient',
        },
        cancellationReason: null,
        doctorNotes: null,
      };
      state.appointments.push(appt);
      return json(route, ok(appt), 201);
    }

    if (method === 'GET' && path.startsWith('/Appointments/patient/')) {
      const items = state.appointments.filter((a) => a.patientId === state.session?.patientId);
      return json(route, ok({ items, pageNumber: 1, pageSize: 50, totalCount: items.length, totalPages: 1 }));
    }

    if (method === 'GET' && path.startsWith('/Appointments/doctor/')) {
      const items = state.appointments.filter((a) => a.doctorId === state.session?.doctorId || a.doctorId === DOCTOR.id);
      return json(route, ok({ items, pageNumber: 1, pageSize: 100, totalCount: items.length, totalPages: 1 }));
    }

    const apptIdMatch = path.match(/^\/Appointments\/(\d+)$/);
    if (method === 'GET' && apptIdMatch) {
      const appt = state.appointments.find((a) => a.id === Number(apptIdMatch[1]));
      if (!appt) return json(route, fail('Not found', 404), 404);
      return json(route, ok(appt));
    }

    const cancelMatch = path.match(/^\/Appointments\/(\d+)\/cancel$/);
    if (method === 'PUT' && cancelMatch) {
      const appt = state.appointments.find((a) => a.id === Number(cancelMatch[1]));
      if (!appt) return json(route, fail('Not found', 404), 404);
      appt.status = 'Cancelled';
      appt.cancellationReason = body?.cancellationReason || 'Cancelled in E2E';
      return json(route, ok(null, 'Cancelled'));
    }

    const confirmMatch = path.match(/^\/Appointments\/(\d+)\/confirm$/);
    if (method === 'PUT' && confirmMatch) {
      const appt = state.appointments.find((a) => a.id === Number(confirmMatch[1]));
      if (!appt) return json(route, fail('Not found', 404), 404);
      appt.status = 'Confirmed';
      return json(route, ok(null, 'Confirmed'));
    }

    const completeMatch = path.match(/^\/Appointments\/(\d+)\/complete$/);
    if (method === 'PUT' && completeMatch) {
      const appt = state.appointments.find((a) => a.id === Number(completeMatch[1]));
      if (!appt) return json(route, fail('Not found', 404), 404);
      appt.status = 'Completed';
      appt.doctorNotes = body?.doctorNotes || '';
      return json(route, ok(null, 'Completed'));
    }

    // ── Payments ──────────────────────────────────────────
    if (method === 'POST' && path === '/Payments/create-intent') {
      return json(route, ok({ clientSecret: 'pi_e2e_secret_secret', paymentIntentId: 'pi_e2e' }));
    }

    if (method === 'POST' && path === '/Payments/process') {
      const id = Number(body?.appointmentId);
      state.paid.add(id);
      const appt = state.appointments.find((a) => a.id === id);
      // After successful payment the real flow confirms; keep Pending for doctor confirm step,
      // but mark paid for UI badges.
      if (appt) appt.paymentStatus = 'Succeeded';
      return json(route, ok({ status: 'Succeeded' }, 'Payment processed'));
    }

    if (method === 'GET' && path.startsWith('/Payments/appointment/')) {
      const id = Number(path.split('/').pop());
      if (state.paid.has(id)) return json(route, ok({ status: 'Succeeded' }));
      return json(route, ok(null));
    }

    // ── Notifications (navbar poll) ───────────────────────
    if (method === 'GET' && path === '/Notifications/unread-count') {
      return json(route, ok({ count: 0 }));
    }
    if (method === 'GET' && path === '/Notifications') {
      return json(route, ok({
        items: [],
        unreadCount: 0,
        totalCount: 0,
        pageNumber: 1,
        totalPages: 1,
      }));
    }

    // ── Calendar ICS (optional) ───────────────────────────
    if (method === 'GET' && path.endsWith('/calendar.ics')) {
      return route.fulfill({
        status: 200,
        contentType: 'text/calendar',
        body: 'BEGIN:VCALENDAR\nVERSION:2.0\nEND:VCALENDAR\n',
      });
    }

    // Unhandled → explicit 404 so tests fail loudly
    return json(route, fail(`E2E mock has no handler for ${method} ${path}`, 404), 404);
  });

  /**
   * Seed a pending appointment for the doctor flow without going through patient UI.
   */
  state.seedPendingAppointment = () => {
    const id = state.nextApptId++;
    const appt = {
      id,
      status: 'Pending',
      referenceCode: `REF-E2E-${id}`,
      patientId: 1,
      doctorId: DOCTOR.id,
      scheduledTime: '2099-06-15T10:00:00.000Z',
      scheduledDate: '2099-06-15',
      scheduledTimeFormatted: '10:00 AM',
      reason: 'Annual checkup for E2E doctor workflow',
      appointmentType: 'Standard',
      consultationFeeAmount: 80,
      consultationFeeCurrency: 'USD',
      doctor: { id: DOCTOR.id, fullName: DOCTOR.fullName },
      patient: { fullName: 'Pat Patient' },
      cancellationReason: null,
      doctorNotes: null,
    };
    state.appointments.push(appt);
    return appt;
  };

  return state;
}

export { DOCTOR };
