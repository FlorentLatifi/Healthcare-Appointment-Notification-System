import { useState, useEffect, useMemo } from 'react';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';

const s = {
  wrapper: { maxWidth: 1000, margin: '24px auto', padding: '0 16px' },
  tabs: { display: 'flex', gap: 8, marginBottom: 20 },
  tab: (active) => ({
    padding: '8px 20px', fontSize: 14, borderRadius: 6, border: '1px solid #ddd',
    background: active ? '#2563eb' : '#f9f9f9', color: active ? '#fff' : '#333', cursor: 'pointer', fontWeight: active ? 600 : 400,
  }),
  searchBar: { display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' },
  searchInput: { padding: '8px 12px', borderRadius: 6, border: '1px solid #ccc', flex: 1, minWidth: 200 },
  addBtn: { padding: '8px 16px', background: '#059669', color: '#fff', border: 'none', borderRadius: 6, cursor: 'pointer', whiteSpace: 'nowrap' },
  table: { width: '100%', borderCollapse: 'collapse', fontSize: 14 },
  th: { textAlign: 'left', padding: '10px 12px', borderBottom: '2px solid #ddd', background: '#f9fafb', fontWeight: 600 },
  td: { padding: '10px 12px', borderBottom: '1px solid #eee' },
  badge: (bg, color) => ({ display: 'inline-block', fontSize: 11, padding: '2px 8px', borderRadius: 10, background: bg, color, fontWeight: 600 }),
  loading: { textAlign: 'center', padding: 40, color: '#888' },
  empty: { textAlign: 'center', padding: 40, color: '#888' },
  overlay: { position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 },
  modal: { background: '#fff', borderRadius: 8, padding: 24, width: 500, maxWidth: '90vw', maxHeight: '90vh', overflowY: 'auto' },
  field: { marginBottom: 12 },
  label: { display: 'block', fontWeight: 600, fontSize: 13, marginBottom: 3 },
  input: { width: '100%', padding: '7px 10px', borderRadius: 6, border: '1px solid #ccc' },
  select: { width: '100%', padding: '7px 10px', borderRadius: 6, border: '1px solid #ccc', background: '#fff' },
  row: { display: 'flex', gap: 10 },
  half: { flex: 1 },
  modalActions: { display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 },
  pagination: { display: 'flex', gap: 8, justifyContent: 'center', marginTop: 16, alignItems: 'center' },
  pageBtn: (disabled) => ({ padding: '6px 14px', border: '1px solid #ccc', borderRadius: 6, background: disabled ? '#f3f4f6' : '#fff', cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.5 : 1 }),
};

const SPECIALTIES = ['General', 'Cardiology', 'Dermatology', 'Neurology', 'Pediatrics', 'Orthopedics', 'Radiology', 'Surgery', 'Ophthalmology', 'Psychiatry', 'Urology', 'Other'];

export default function AdminDashboardPage() {
  const [page, setPage] = useState('doctors');
  const [searchTerm, setSearchTerm] = useState('');

  // doctors
  const [doctors, setDoctors] = useState([]);
  const [docsLoading, setDocsLoading] = useState(true);
  const [docPage, setDocPage] = useState(1);
  const [docTotalPages, setDocTotalPages] = useState(1);
  const [showDocForm, setShowDocForm] = useState(false);
  const [docForm, setDocForm] = useState({
    firstName: '', lastName: '', email: '', phoneNumber: '',
    licenseNumber: '', specialty: 'General', consultationFeeAmount: '',
    consultationFeeCurrency: 'USD', yearsOfExperience: '',
  });
  const [docSubmitting, setDocSubmitting] = useState(false);

  // patients
  const [patients, setPatients] = useState([]);
  const [patsLoading, setPatsLoading] = useState(true);

  const fetchDoctors = async (p = 1) => {
    setDocsLoading(true);
    try {
      const { data } = await apiClient.get('/Doctors', { params: { pageNumber: p, pageSize: 20 } });
      if (data.success) { setDoctors(data.data.items); setDocPage(data.data.pageNumber); setDocTotalPages(data.data.totalPages); }
      else toast.error(data.message || 'Failed to load doctors');
    } catch { toast.error('Failed to load doctors'); }
    finally { setDocsLoading(false); }
  };

  const fetchPatients = async () => {
    setPatsLoading(true);
    try {
      const params = {};
      if (searchTerm.trim()) params.term = searchTerm.trim();
      const endpoint = searchTerm.trim() ? '/Patients/search' : '/Patients';
      const { data } = await apiClient.get(endpoint, { params: { ...params, pageSize: searchTerm.trim() ? 100 : 50 } });
      if (data.success) setPatients(Array.isArray(data.data) ? data.data : data.data.items || []);
      else toast.error(data.message || 'Failed to load patients');
    } catch { toast.error('Failed to load patients'); }
    finally { setPatsLoading(false); }
  };

  useEffect(() => { if (page === 'doctors') fetchDoctors(1); }, [page]);
  useEffect(() => { if (page === 'patients') fetchPatients(); }, [page, searchTerm]);

  const handleCreateDoctor = async (e) => {
    e.preventDefault();
    setDocSubmitting(true);
    try {
      const { data } = await apiClient.post('/Doctors', {
        ...docForm,
        consultationFeeAmount: parseFloat(docForm.consultationFeeAmount) || 0,
        yearsOfExperience: parseInt(docForm.yearsOfExperience) || 0,
      });
      if (data.success) {
        toast.success('Doctor created');
        setShowDocForm(false);
        setDocForm({ firstName: '', lastName: '', email: '', phoneNumber: '', licenseNumber: '', specialty: 'General', consultationFeeAmount: '', consultationFeeCurrency: 'USD', yearsOfExperience: '' });
        fetchDoctors(1);
      } else toast.error(data.errors?.join('. ') || data.message || 'Failed to create doctor');
    } catch (err) { toast.error(err.response?.data?.errors?.join('. ') || 'Failed'); }
    finally { setDocSubmitting(false); }
  };

  const setF = (field) => (e) => setDocForm((p) => ({ ...p, [field]: e.target.value }));

  return (
    <div style={s.wrapper}>
      <h1>Admin Dashboard</h1>
      <div style={s.tabs}>
        <button style={s.tab(page === 'doctors')} onClick={() => setPage('doctors')}>Doctors</button>
        <button style={s.tab(page === 'patients')} onClick={() => setPage('patients')}>Patients</button>
      </div>

      {page === 'doctors' && (
        <>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h3 style={{ margin: 0 }}>All Doctors</h3>
            <button style={s.addBtn} onClick={() => setShowDocForm(true)}>+ Add Doctor</button>
          </div>
          {docsLoading ? <div style={s.loading}>Loading...</div> : doctors.length === 0 ? <div style={s.empty}>No doctors found.</div> : (
            <>
              <table style={s.table}>
                <thead><tr>
                  <th style={s.th}>Name</th><th style={s.th}>Email</th><th style={s.th}>Specialty</th><th style={s.th}>Fee</th><th style={s.th}>Status</th>
                </tr></thead>
                <tbody>
                  {doctors.map((doc) => (
                    <tr key={doc.id}>
                      <td style={s.td}>Dr. {doc.fullName}</td>
                      <td style={s.td}>{doc.email}</td>
                      <td style={s.td}>{doc.specialties?.join(', ')}</td>
                      <td style={s.td}>{doc.consultationFeeCurrency} {doc.consultationFeeAmount}</td>
                      <td style={s.td}>
                        {doc.isActive ? <span style={s.badge('#d1fae5', '#065f46')}>Active</span> : <span style={s.badge('#fee2e2', '#991b1b')}>Inactive</span>}
                        {doc.isAcceptingPatients && <span style={{ ...s.badge('#dbeafe', '#1e40af'), marginLeft: 6 }}>Accepting</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div style={s.pagination}>
                <button style={s.pageBtn(docPage <= 1)} disabled={docPage <= 1} onClick={() => fetchDoctors(docPage - 1)}>Prev</button>
                <span style={{ fontSize: 14, color: '#666' }}>Page {docPage} of {docTotalPages}</span>
                <button style={s.pageBtn(docPage >= docTotalPages)} disabled={docPage >= docTotalPages} onClick={() => fetchDoctors(docPage + 1)}>Next</button>
              </div>
            </>
          )}

          {showDocForm && (
            <div style={s.overlay} onClick={() => setShowDocForm(false)}>
              <div style={s.modal} onClick={(e) => e.stopPropagation()}>
                <h3 style={{ margin: '0 0 16px' }}>Add Doctor</h3>
                <form onSubmit={handleCreateDoctor}>
                  <div style={s.row}>
                    <div style={{ ...s.field, ...s.half }}><label style={s.label}>First Name</label><input style={s.input} value={docForm.firstName} onChange={setF('firstName')} required /></div>
                    <div style={{ ...s.field, ...s.half }}><label style={s.label}>Last Name</label><input style={s.input} value={docForm.lastName} onChange={setF('lastName')} required /></div>
                  </div>
                  <div style={s.field}><label style={s.label}>Email</label><input style={s.input} type="email" value={docForm.email} onChange={setF('email')} required /></div>
                  <div style={s.field}><label style={s.label}>Phone</label><input style={s.input} value={docForm.phoneNumber} onChange={setF('phoneNumber')} required /></div>
                  <div style={s.field}><label style={s.label}>License Number</label><input style={s.input} value={docForm.licenseNumber} onChange={setF('licenseNumber')} required /></div>
                  <div style={s.row}>
                    <div style={{ ...s.field, ...s.half }}><label style={s.label}>Specialty</label><select style={s.select} value={docForm.specialty} onChange={setF('specialty')}>{SPECIALTIES.map((sp) => <option key={sp}>{sp}</option>)}</select></div>
                    <div style={{ ...s.field, ...s.half }}><label style={s.label}>Years Exp.</label><input style={s.input} type="number" min="0" value={docForm.yearsOfExperience} onChange={setF('yearsOfExperience')} required /></div>
                  </div>
                  <div style={s.row}>
                    <div style={{ ...s.field, ...s.half }}><label style={s.label}>Fee Amount</label><input style={s.input} type="number" step="0.01" min="0" value={docForm.consultationFeeAmount} onChange={setF('consultationFeeAmount')} required /></div>
                    <div style={{ ...s.field, ...s.half }}><label style={s.label}>Currency</label><select style={s.select} value={docForm.consultationFeeCurrency} onChange={setF('consultationFeeCurrency')}><option>USD</option><option>EUR</option><option>GBP</option></select></div>
                  </div>
                  <div style={s.modalActions}>
                    <button type="button" style={{ padding: '8px 16px', border: '1px solid #ccc', borderRadius: 6, background: '#fff' }} onClick={() => setShowDocForm(false)}>Cancel</button>
                    <button type="submit" style={{ padding: '8px 16px', background: '#059669', color: '#fff', border: 'none', borderRadius: 6 }} disabled={docSubmitting}>{docSubmitting ? 'Creating...' : 'Create'}</button>
                  </div>
                </form>
              </div>
            </div>
          )}
        </>
      )}

      {page === 'patients' && (
        <>
          <div style={s.searchBar}>
            <input style={s.searchInput} placeholder="Search patients by name..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            <button style={s.addBtn} onClick={fetchPatients}>Search</button>
          </div>
          {patsLoading ? <div style={s.loading}>Loading...</div> : patients.length === 0 ? <div style={s.empty}>No patients found.</div> : (
            <table style={s.table}>
              <thead><tr><th style={s.th}>Name</th><th style={s.th}>Email</th><th style={s.th}>Phone</th><th style={s.th}>Gender</th><th style={s.th}>DOB</th><th style={s.th}>Status</th></tr></thead>
              <tbody>
                {patients.map((p) => (
                  <tr key={p.id}>
                    <td style={s.td}>{p.fullName}</td>
                    <td style={s.td}>{p.email}</td>
                    <td style={s.td}>{p.phoneNumber}</td>
                    <td style={s.td}>{p.gender}</td>
                    <td style={s.td}>{p.dateOfBirth?.split('T')[0]}</td>
                    <td style={s.td}>{p.isActive ? <span style={s.badge('#d1fae5', '#065f46')}>Active</span> : <span style={s.badge('#fee2e2', '#991b1b')}>Inactive</span>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}
    </div>
  );
}
