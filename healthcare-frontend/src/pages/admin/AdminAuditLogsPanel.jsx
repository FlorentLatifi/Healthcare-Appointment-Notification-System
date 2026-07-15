import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import apiClient from '../../services/apiClient';
import {
  Badge,
  Button,
  EmptyState,
  Input,
  Modal,
  Select,
  Spinner,
  Table,
  TableScroll,
  Td,
  Th,
  Tr,
} from '../../components/ui';
import { ChevronLeft, ChevronRight, Search } from 'lucide-react';
import {
  defaultDateRange,
  formatDateTime,
  outcomeBadgeStatus,
  toApiDateBounds,
} from './dateRange';

const OUTCOMES = ['', 'Success', 'Failure'];
const RESOURCE_TYPES = ['', 'Patient', 'Appointment', 'Payment', 'User', 'Doctor'];
const COMMON_ACTIONS = [
  '',
  'LoginSucceeded',
  'LoginFailed',
  'BookAppointment',
  'CreatePatient',
  'GetPatientById',
  'ProcessPayment',
  'CreatePaymentIntent',
  'AnonymizePatient',
  'PromoteToAdmin',
];

/**
 * Admin audit log browser: GET /AuditLogs with filters + pagination.
 */
export default function AdminAuditLogsPanel() {
  const defaults = defaultDateRange(30);
  const [filters, setFilters] = useState({
    from: defaults.from,
    to: defaults.to,
    action: '',
    resourceType: '',
    outcome: '',
    actorUserId: '',
    correlationId: '',
  });
  const [applied, setApplied] = useState(filters);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [loading, setLoading] = useState(true);
  const [items, setItems] = useState([]);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [detail, setDetail] = useState(null);

  const setFilter = (field) => (e) => setFilters((prev) => ({ ...prev, [field]: e.target.value }));

  const load = useCallback(async (pageNumber = 1, activeFilters = applied) => {
    setLoading(true);
    try {
      const { dateFrom, dateTo } = toApiDateBounds(activeFilters.from, activeFilters.to);
      const params = {
        pageNumber,
        pageSize,
      };
      if (dateFrom) params.from = dateFrom;
      if (dateTo) params.to = dateTo;
      if (activeFilters.action?.trim()) params.action = activeFilters.action.trim();
      if (activeFilters.resourceType?.trim()) params.resourceType = activeFilters.resourceType.trim();
      if (activeFilters.outcome?.trim()) params.outcome = activeFilters.outcome.trim();
      if (activeFilters.correlationId?.trim()) params.correlationId = activeFilters.correlationId.trim();
      const actor = activeFilters.actorUserId?.trim();
      if (actor && Number.isFinite(Number(actor))) params.actorUserId = Number(actor);

      const { data } = await apiClient.get('/AuditLogs', { params });
      if (!data.success) {
        toast.error(data.message || 'Failed to load audit logs');
        setItems([]);
        return;
      }

      const payload = data.data || {};
      setItems(Array.isArray(payload.items) ? payload.items : []);
      setPage(payload.pageNumber || pageNumber);
      setTotalPages(Math.max(1, payload.totalPages || 1));
      setTotalCount(payload.totalCount ?? 0);
    } catch (err) {
      setItems([]);
      toast.error(err.response?.data?.message || 'Failed to load audit logs');
    } finally {
      setLoading(false);
    }
  }, [applied, pageSize]);

  useEffect(() => {
    load(page, applied);
  }, [load, page, applied]);

  const applyFilters = (e) => {
    e?.preventDefault?.();
    if (filters.from && filters.to && filters.from > filters.to) {
      toast.error('Start date must be on or before end date');
      return;
    }
    setPage(1);
    setApplied({ ...filters });
  };

  return (
    <div className="space-y-4" data-testid="admin-audit-panel">
      <form
        onSubmit={applyFilters}
        className="bg-white rounded-xl border border-border-light shadow-card p-4 sm:p-5"
        aria-label="Audit log filters"
      >
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-0 sm:gap-x-3">
          <Input
            label="From"
            type="date"
            name="auditFrom"
            value={filters.from}
            onChange={setFilter('from')}
          />
          <Input
            label="To"
            type="date"
            name="auditTo"
            value={filters.to}
            onChange={setFilter('to')}
          />
          <Select
            label="Action"
            name="auditAction"
            value={filters.action}
            onChange={setFilter('action')}
          >
            {COMMON_ACTIONS.map((a) => (
              <option key={a || 'any-action'} value={a}>
                {a || 'Any action'}
              </option>
            ))}
          </Select>
          <Select
            label="Resource type"
            name="auditResourceType"
            value={filters.resourceType}
            onChange={setFilter('resourceType')}
          >
            {RESOURCE_TYPES.map((t) => (
              <option key={t || 'any-type'} value={t}>
                {t || 'Any resource'}
              </option>
            ))}
          </Select>
          <Select
            label="Outcome"
            name="auditOutcome"
            value={filters.outcome}
            onChange={setFilter('outcome')}
          >
            {OUTCOMES.map((o) => (
              <option key={o || 'any-outcome'} value={o}>
                {o || 'Any outcome'}
              </option>
            ))}
          </Select>
          <Input
            label="Actor user id"
            name="auditActor"
            type="number"
            min="1"
            placeholder="Optional"
            value={filters.actorUserId}
            onChange={setFilter('actorUserId')}
          />
          <Input
            label="Correlation id"
            name="auditCorrelation"
            placeholder="Optional"
            value={filters.correlationId}
            onChange={setFilter('correlationId')}
          />
        </div>
        <div className="flex flex-col sm:flex-row gap-2 sm:justify-end">
          <Button type="submit" className="w-full sm:w-auto" leftIcon={<Search size={14} />}>
            Apply filters
          </Button>
        </div>
      </form>

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-text-muted m-0">
          {totalCount} log{totalCount === 1 ? '' : 's'}
        </p>
      </div>

      {loading ? (
        <Spinner />
      ) : items.length === 0 ? (
        <EmptyState message="No audit logs match these filters." />
      ) : (
        <>
          <TableScroll label="Audit logs">
            <Table className="min-w-[40rem] sm:min-w-[56rem]">
              <thead>
                <tr>
                  <Th>When</Th>
                  <Th>Action</Th>
                  <Th>Resource</Th>
                  <Th>Outcome</Th>
                  <Th>Actor</Th>
                  <Th>Details</Th>
                </tr>
              </thead>
              <tbody>
                {items.map((row) => {
                  const action = row.action || row.eventType || '—';
                  const resourceType = row.resourceType || row.entityType || '—';
                  const resourceId = row.resourceId ?? row.entityId;
                  const actorId = row.actorUserId ?? row.userId;
                  const outcome = row.outcome || '—';
                  const details = row.details || '';
                  return (
                    <Tr key={row.id}>
                      <Td className="whitespace-nowrap text-text-muted text-xs sm:text-sm">
                        {formatDateTime(row.occurredOn || row.createdAt)}
                      </Td>
                      <Td className="font-medium whitespace-nowrap">{action}</Td>
                      <Td className="whitespace-nowrap">
                        {resourceType}
                        {resourceId != null ? ` #${resourceId}` : ''}
                      </Td>
                      <Td>
                        <Badge status={outcomeBadgeStatus(outcome)}>{outcome}</Badge>
                      </Td>
                      <Td className="whitespace-nowrap">
                        {actorId != null ? (
                          <span>
                            #{actorId}
                            {row.actorRole ? (
                              <span className="text-text-muted"> ({row.actorRole})</span>
                            ) : null}
                          </span>
                        ) : (
                          <span className="text-text-muted">—</span>
                        )}
                      </Td>
                      <Td className="max-w-[14rem]">
                        <button
                          type="button"
                          className="text-left text-sm text-primary hover:underline bg-transparent border-none p-0 cursor-pointer max-w-full truncate block"
                          title={details || 'View details'}
                          onClick={() => setDetail(row)}
                        >
                          {details ? truncate(details, 60) : 'View'}
                        </button>
                      </Td>
                    </Tr>
                  );
                })}
              </tbody>
            </Table>
          </TableScroll>

          <div className="flex flex-wrap items-center justify-center gap-2 sm:gap-3">
            <Button
              variant="secondary"
              size="sm"
              disabled={page <= 1 || loading}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              <ChevronLeft size={14} className="mr-1" />Prev
            </Button>
            <span className="text-xs text-text-muted">
              Page {page} of {totalPages}
            </span>
            <Button
              variant="secondary"
              size="sm"
              disabled={page >= totalPages || loading}
              onClick={() => setPage((p) => p + 1)}
            >
              Next<ChevronRight size={14} className="ml-1" />
            </Button>
          </div>
        </>
      )}

      <Modal
        open={!!detail}
        onClose={() => setDetail(null)}
        title="Audit log detail"
        footer={
          <Button variant="secondary" className="w-full sm:w-auto" onClick={() => setDetail(null)}>
            Close
          </Button>
        }
      >
        {detail && (
          <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-3 m-0 text-sm">
            <Detail label="Id" value={detail.id} />
            <Detail label="When" value={formatDateTime(detail.occurredOn || detail.createdAt)} />
            <Detail label="Action" value={detail.action || detail.eventType} />
            <Detail
              label="Resource"
              value={`${detail.resourceType || detail.entityType || '—'}${
                (detail.resourceId ?? detail.entityId) != null
                  ? ` #${detail.resourceId ?? detail.entityId}`
                  : ''
              }`}
            />
            <Detail label="Outcome" value={detail.outcome} />
            <Detail
              label="Actor"
              value={
                (detail.actorUserId ?? detail.userId) != null
                  ? `#${detail.actorUserId ?? detail.userId}${detail.actorRole ? ` (${detail.actorRole})` : ''}`
                  : '—'
              }
            />
            <Detail label="Client IP" value={detail.clientIp || '—'} />
            <Detail label="Correlation" value={detail.correlationId || '—'} />
            <div className="sm:col-span-2">
              <dt className="text-xs uppercase tracking-wider text-text-muted m-0 mb-1">Details</dt>
              <dd className="m-0 p-3 rounded-md bg-surface border border-border-light whitespace-pre-wrap break-words text-text">
                {detail.details || '—'}
              </dd>
            </div>
            {detail.userAgent && (
              <div className="sm:col-span-2">
                <dt className="text-xs uppercase tracking-wider text-text-muted m-0 mb-1">User agent</dt>
                <dd className="m-0 text-text-muted break-all">{detail.userAgent}</dd>
              </div>
            )}
          </dl>
        )}
      </Modal>
    </div>
  );
}

function Detail({ label, value }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs uppercase tracking-wider text-text-muted m-0 mb-1">{label}</dt>
      <dd className="m-0 text-text break-words">{value ?? '—'}</dd>
    </div>
  );
}

function truncate(text, max) {
  if (!text || text.length <= max) return text;
  return `${text.slice(0, max - 1)}…`;
}
