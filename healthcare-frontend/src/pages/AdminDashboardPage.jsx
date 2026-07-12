import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { Button, Badge, Spinner, EmptyState, Modal, Input, Select, PageHeader } from '../components/ui';
import { Table, TableScroll, Th, Td, Tr } from '../components/ui';
import { UserPlus, ChevronLeft, ChevronRight } from 'lucide-react';

const SPECIALTIES = ['General', 'Cardiology', 'Dermatology', 'Neurology', 'Pediatrics', 'Orthopedics', 'Radiology', 'Surgery', 'Ophthalmology', 'Psychiatry', 'Urology', 'Other'];

export default function AdminDashboardPage() {
  const [page, setPage] = useState('doctors');
  const [searchTerm, setSearchTerm] = useState('');

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
    <div className="max-w-5xl mx-auto px-4 sm:px-6 py-6 sm:py-12">
      <PageHeader title="Admin Dashboard" />

      <div className="flex flex-wrap gap-2 mb-4 sm:mb-6">
        <Button
          variant={page === 'doctors' ? 'primary' : 'secondary'}
          className="flex-1 sm:flex-none"
          onClick={() => setPage('doctors')}
        >
          Doctors
        </Button>
        <Button
          variant={page === 'patients' ? 'primary' : 'secondary'}
          className="flex-1 sm:flex-none"
          onClick={() => setPage('patients')}
        >
          Patients
        </Button>
      </div>

      {page === 'doctors' && (
        <>
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-4">
            <h3 className="text-base font-medium text-text m-0">All Doctors</h3>
            <Button size="sm" className="w-full sm:w-auto" leftIcon={<UserPlus size={14} />} onClick={() => setShowDocForm(true)}>
              Add Doctor
            </Button>
          </div>

          {docsLoading ? <Spinner /> : doctors.length === 0 ? <EmptyState message="No doctors found." /> : (
            <>
              <TableScroll>
                <Table>
                  <thead>
                    <tr>
                      <Th>Name</Th>
                      <Th>Email</Th>
                      <Th>Specialty</Th>
                      <Th>Fee</Th>
                      <Th>Status</Th>
                    </tr>
                  </thead>
                  <tbody>
                    {doctors.map((doc) => (
                      <Tr key={doc.id}>
                        <Td className="font-medium whitespace-nowrap">Dr. {doc.fullName}</Td>
                        <Td className="text-text-muted">{doc.email}</Td>
                        <Td className="max-w-[10rem] truncate" title={doc.specialties?.join(', ')}>{doc.specialties?.join(', ')}</Td>
                        <Td className="whitespace-nowrap">{doc.consultationFeeCurrency} {doc.consultationFeeAmount}</Td>
                        <Td>
                          <div className="flex flex-wrap gap-1">
                            {doc.isActive ? <Badge status="Confirmed">Active</Badge> : <Badge status="Cancelled">Inactive</Badge>}
                            {doc.isAcceptingPatients && <Badge status="Scheduled">Accepting</Badge>}
                          </div>
                        </Td>
                      </Tr>
                    ))}
                  </tbody>
                </Table>
              </TableScroll>
              <div className="flex flex-wrap items-center justify-center gap-2 sm:gap-3 mt-4">
                <Button variant="secondary" size="sm" disabled={docPage <= 1} onClick={() => fetchDoctors(docPage - 1)}>
                  <ChevronLeft size={14} className="mr-1" />Prev
                </Button>
                <span className="text-xs text-text-muted">Page {docPage} of {docTotalPages}</span>
                <Button variant="secondary" size="sm" disabled={docPage >= docTotalPages} onClick={() => fetchDoctors(docPage + 1)}>
                  Next<ChevronRight size={14} className="ml-1" />
                </Button>
              </div>
            </>
          )}

          <Modal
            open={showDocForm}
            onClose={() => setShowDocForm(false)}
            title="Add Doctor"
            footer={
              <>
                <Button variant="secondary" type="button" className="w-full sm:w-auto" onClick={() => setShowDocForm(false)}>Cancel</Button>
                <Button type="submit" form="add-doctor-form" loading={docSubmitting} className="w-full sm:w-auto">Create</Button>
              </>
            }
          >
            <form id="add-doctor-form" onSubmit={handleCreateDoctor}>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-3">
                <Input label="First Name" value={docForm.firstName} onChange={setF('firstName')} required />
                <Input label="Last Name" value={docForm.lastName} onChange={setF('lastName')} required />
              </div>
              <Input label="Email" type="email" value={docForm.email} onChange={setF('email')} required />
              <Input label="Phone" value={docForm.phoneNumber} onChange={setF('phoneNumber')} required />
              <Input label="License Number" value={docForm.licenseNumber} onChange={setF('licenseNumber')} required />
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-3">
                <Select label="Specialty" value={docForm.specialty} onChange={setF('specialty')}>
                  {SPECIALTIES.map((sp) => <option key={sp}>{sp}</option>)}
                </Select>
                <Input label="Years Exp." type="number" min="0" value={docForm.yearsOfExperience} onChange={setF('yearsOfExperience')} required />
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-3">
                <Input label="Fee Amount" type="number" step="0.01" min="0" value={docForm.consultationFeeAmount} onChange={setF('consultationFeeAmount')} required />
                <Select label="Currency" value={docForm.consultationFeeCurrency} onChange={setF('consultationFeeCurrency')}>
                  <option>USD</option><option>EUR</option><option>GBP</option>
                </Select>
              </div>
            </form>
          </Modal>
        </>
      )}

      {page === 'patients' && (
        <>
          <div className="flex flex-col sm:flex-row gap-2 mb-4">
            <div className="flex-1 sm:max-w-xs min-w-0">
              <Input
                placeholder="Search patients by name..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            <Button variant="primary" size="sm" className="w-full sm:w-auto shrink-0" onClick={fetchPatients}>Search</Button>
          </div>

          {patsLoading ? <Spinner /> : patients.length === 0 ? <EmptyState message="No patients found." /> : (
            <TableScroll>
              <Table className="min-w-[40rem]">
                <thead>
                  <tr>
                    <Th>Name</Th>
                    <Th>Email</Th>
                    <Th>Phone</Th>
                    <Th>Gender</Th>
                    <Th>DOB</Th>
                    <Th>Status</Th>
                  </tr>
                </thead>
                <tbody>
                  {patients.map((p) => (
                    <Tr key={p.id}>
                      <Td className="font-medium whitespace-nowrap">{p.fullName}</Td>
                      <Td className="text-text-muted">{p.email}</Td>
                      <Td className="whitespace-nowrap">{p.phoneNumber}</Td>
                      <Td>{p.gender}</Td>
                      <Td className="text-text-muted whitespace-nowrap">{p.dateOfBirth?.split('T')[0]}</Td>
                      <Td>{p.isActive ? <Badge status="Confirmed">Active</Badge> : <Badge status="Cancelled">Inactive</Badge>}</Td>
                    </Tr>
                  ))}
                </tbody>
              </Table>
            </TableScroll>
          )}
        </>
      )}
    </div>
  );
}
