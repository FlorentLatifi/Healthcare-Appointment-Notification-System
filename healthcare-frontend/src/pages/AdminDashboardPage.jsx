import { useState, useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { Button, Badge, Spinner, EmptyState, Modal, Input, Select, PageHeader, Card } from '../components/ui';
import { Table, TableScroll, Th, Td, Tr } from '../components/ui';
import {
  UserPlus, ChevronLeft, ChevronRight, DollarSign, Percent, CalendarRange,
  BarChart3, ScrollText, Users, Stethoscope, ArrowRight,
} from 'lucide-react';
import { SPECIALTIES, DEFAULT_SPECIALTY } from '../constants/specialties';
import { defaultDateRange, formatMoney, toApiDateBounds } from './admin/dateRange';

function sectionFromPath(pathname) {
  if (pathname.includes('/patients')) return 'patients';
  return 'doctors';
}

export default function AdminDashboardPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const page = sectionFromPath(location.pathname);
  const [searchTerm, setSearchTerm] = useState('');

  const [doctors, setDoctors] = useState([]);
  const [docsLoading, setDocsLoading] = useState(true);
  const [docPage, setDocPage] = useState(1);
  const [docTotalPages, setDocTotalPages] = useState(1);
  const [showDocForm, setShowDocForm] = useState(false);
  const [docForm, setDocForm] = useState({
    firstName: '', lastName: '', email: '', phoneNumber: '',
    licenseNumber: '', specialty: DEFAULT_SPECIALTY, consultationFeeAmount: '',
    consultationFeeCurrency: 'USD', yearsOfExperience: '',
  });
  const [docSubmitting, setDocSubmitting] = useState(false);

  const [patients, setPatients] = useState([]);
  const [patsLoading, setPatsLoading] = useState(true);

  const [kpis, setKpis] = useState({ revenue: null, noShow: null, volume: null, loading: true });

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

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const range = defaultDateRange(30);
      const { dateFrom, dateTo } = toApiDateBounds(range.from, range.to);
      try {
        const [rev, ns, vol] = await Promise.all([
          apiClient.get('/Analytics/revenue', { params: { dateFrom, dateTo } }),
          apiClient.get('/Analytics/no-show-rate', { params: { dateFrom, dateTo } }),
          apiClient.get('/Analytics/volume', { params: { dateFrom, dateTo, groupBy: 'day' } }),
        ]);
        if (cancelled) return;
        const volumeItems = vol.data?.success ? (vol.data.data?.items || []) : [];
        const volumeTotal = volumeItems.reduce(
          (acc, row) => acc + (row.created || 0) + (row.confirmed || 0) + (row.cancelled || 0),
          0,
        );
        setKpis({
          revenue: rev.data?.success ? rev.data.data : null,
          noShow: ns.data?.success ? ns.data.data : null,
          volume: vol.data?.success ? { total: volumeTotal, groupBy: vol.data.data?.groupBy } : null,
          loading: false,
        });
      } catch {
        if (!cancelled) setKpis((prev) => ({ ...prev, loading: false }));
      }
    })();
    return () => { cancelled = true; };
  }, []);

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
        setDocForm({ firstName: '', lastName: '', email: '', phoneNumber: '', licenseNumber: '', specialty: DEFAULT_SPECIALTY, consultationFeeAmount: '', consultationFeeCurrency: 'USD', yearsOfExperience: '' });
        fetchDoctors(1);
      } else toast.error(data.errors?.join('. ') || data.message || 'Failed to create doctor');
    } catch (err) { toast.error(err.response?.data?.errors?.join('. ') || 'Failed'); }
    finally { setDocSubmitting(false); }
  };

  const setF = (field) => (e) => setDocForm((p) => ({ ...p, [field]: e.target.value }));

  const volumeValue = kpis.volume?.total != null ? String(kpis.volume.total) : '—';
  const noShowValue = kpis.noShow == null
    ? '—'
    : `${Number(kpis.noShow.noShowRatePercent ?? 0).toFixed(1)}%`;
  const revenueValue = kpis.revenue
    ? formatMoney(kpis.revenue.totalRevenue, kpis.revenue.currency)
    : '—';

  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <PageHeader
        title="Admin catalog"
        subtitle="Overview of system health, plus tools to manage doctors and patients."
      />

      <section className="mb-6 sm:mb-8" aria-labelledby="admin-kpis-heading">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 mb-3">
          <h2 id="admin-kpis-heading" className="text-sm font-medium text-text-secondary uppercase tracking-wider m-0">
            Last 30 days
          </h2>
          <Button
            variant="ghost"
            size="sm"
            className="w-full sm:w-auto justify-center"
            rightIcon={<ArrowRight size={14} />}
            onClick={() => navigate('/admin/analytics')}
          >
            Open analytics
          </Button>
        </div>
        {kpis.loading ? (
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <Spinner />
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3" data-testid="admin-kpi-cards">
            <Card className="!p-3 sm:!p-4" hover onClick={() => navigate('/admin/analytics')}>
              <div className="flex items-start gap-3">
                <div className="rounded-lg bg-surface p-2 text-primary shrink-0"><DollarSign size={18} /></div>
                <div className="min-w-0">
                  <p className="text-xs uppercase tracking-wider text-text-muted m-0">Revenue</p>
                  <p className="text-xl font-semibold text-text m-0 mt-1 break-words">{revenueValue}</p>
                </div>
              </div>
            </Card>
            <Card className="!p-3 sm:!p-4" hover onClick={() => navigate('/admin/analytics')}>
              <div className="flex items-start gap-3">
                <div className="rounded-lg bg-surface p-2 text-primary shrink-0"><Percent size={18} /></div>
                <div className="min-w-0">
                  <p className="text-xs uppercase tracking-wider text-text-muted m-0">No-show rate</p>
                  <p className="text-xl font-semibold text-text m-0 mt-1">{noShowValue}</p>
                </div>
              </div>
            </Card>
            <Card className="!p-3 sm:!p-4" hover onClick={() => navigate('/admin/analytics')}>
              <div className="flex items-start gap-3">
                <div className="rounded-lg bg-surface p-2 text-primary shrink-0"><CalendarRange size={18} /></div>
                <div className="min-w-0">
                  <p className="text-xs uppercase tracking-wider text-text-muted m-0">Volume activity</p>
                  <p className="text-xl font-semibold text-text m-0 mt-1">{volumeValue}</p>
                </div>
              </div>
            </Card>
          </div>
        )}
      </section>

      <section className="mb-6 sm:mb-8" aria-labelledby="admin-quick-links-heading">
        <h2 id="admin-quick-links-heading" className="text-sm font-medium text-text-secondary uppercase tracking-wider mb-3">
          Quick links
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          {[
            { title: 'Analytics', desc: 'Full KPI reports', path: '/admin/analytics', icon: <BarChart3 size={18} /> },
            { title: 'Audit logs', desc: 'Security & PHI trail', path: '/admin/audit-logs', icon: <ScrollText size={18} /> },
            { title: 'Doctors', desc: 'Catalog & fees', path: '/admin', icon: <Stethoscope size={18} /> },
            { title: 'Patients', desc: 'Search records', path: '/admin/patients', icon: <Users size={18} /> },
          ].map((link) => (
            <Card key={link.path + link.title} hover onClick={() => navigate(link.path)} className="!p-3 sm:!p-4">
              <div className="flex items-center gap-3 min-w-0">
                <div className="shrink-0 w-9 h-9 rounded-lg bg-surface flex items-center justify-center text-primary">
                  {link.icon}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-medium text-text m-0">{link.title}</p>
                  <p className="text-xs text-text-muted m-0 mt-0.5">{link.desc}</p>
                </div>
              </div>
            </Card>
          ))}
        </div>
      </section>

      <div className="flex flex-wrap gap-2 mb-4 sm:mb-6" role="tablist" aria-label="Catalog sections">
        <Button
          role="tab"
          aria-selected={page === 'doctors'}
          variant={page === 'doctors' ? 'primary' : 'secondary'}
          className="flex-1 sm:flex-none"
          onClick={() => navigate('/admin')}
        >
          Doctors
        </Button>
        <Button
          role="tab"
          aria-selected={page === 'patients'}
          variant={page === 'patients' ? 'primary' : 'secondary'}
          className="flex-1 sm:flex-none"
          onClick={() => navigate('/admin/patients')}
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
              <TableScroll label="Doctors">
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
                  {SPECIALTIES.map((sp) => <option key={sp} value={sp}>{sp}</option>)}
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
          <div className="flex flex-col sm:flex-row gap-2 mb-4 sm:items-end">
            <div className="flex-1 sm:max-w-xs min-w-0">
              <Input
                label="Search patients"
                id="admin-patient-search"
                name="patientSearch"
                placeholder="Search by name..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                autoComplete="off"
              />
            </div>
            <Button variant="primary" size="sm" className="w-full sm:w-auto shrink-0 mb-4" onClick={fetchPatients}>
              Search
            </Button>
          </div>

          {patsLoading ? <Spinner /> : patients.length === 0 ? <EmptyState message="No patients found." /> : (
            <TableScroll label="Patients">
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
