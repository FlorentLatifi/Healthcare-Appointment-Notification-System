import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import apiClient from '../../services/apiClient';
import {
  Badge,
  Button,
  Card,
  EmptyState,
  Input,
  Select,
  Spinner,
  Table,
  TableScroll,
  Td,
  Th,
  Tr,
} from '../../components/ui';
import { DollarSign, Percent, CalendarRange, RefreshCw } from 'lucide-react';
import { defaultDateRange, formatMoney, toApiDateBounds } from './dateRange';

/**
 * Admin analytics: wires GET /Analytics/revenue, /no-show-rate, /volume.
 */
export default function AdminAnalyticsPanel() {
  const defaults = defaultDateRange(30);
  const [from, setFrom] = useState(defaults.from);
  const [to, setTo] = useState(defaults.to);
  const [revenueGroupBy, setRevenueGroupBy] = useState('doctor');
  const [volumeGroupBy, setVolumeGroupBy] = useState('day');
  const [loading, setLoading] = useState(true);
  const [revenue, setRevenue] = useState(null);
  const [noShow, setNoShow] = useState(null);
  const [volume, setVolume] = useState(null);

  const load = useCallback(async () => {
    if (!from || !to) {
      toast.error('Select a valid date range');
      return;
    }
    if (from > to) {
      toast.error('Start date must be on or before end date');
      return;
    }

    setLoading(true);
    const { dateFrom, dateTo } = toApiDateBounds(from, to);
    try {
      const [revRes, nsRes, volRes] = await Promise.all([
        apiClient.get('/Analytics/revenue', {
          params: { dateFrom, dateTo, groupBy: revenueGroupBy || undefined },
        }),
        apiClient.get('/Analytics/no-show-rate', {
          params: { dateFrom, dateTo },
        }),
        apiClient.get('/Analytics/volume', {
          params: { dateFrom, dateTo, groupBy: volumeGroupBy || 'day' },
        }),
      ]);

      if (revRes.data?.success) setRevenue(revRes.data.data);
      else {
        setRevenue(null);
        toast.error(revRes.data?.message || 'Failed to load revenue');
      }

      if (nsRes.data?.success) setNoShow(nsRes.data.data);
      else {
        setNoShow(null);
        toast.error(nsRes.data?.message || 'Failed to load no-show rate');
      }

      if (volRes.data?.success) setVolume(volRes.data.data);
      else {
        setVolume(null);
        toast.error(volRes.data?.message || 'Failed to load volume');
      }
    } catch (err) {
      setRevenue(null);
      setNoShow(null);
      setVolume(null);
      toast.error(err.response?.data?.message || 'Failed to load analytics');
    } finally {
      setLoading(false);
    }
  }, [from, to, revenueGroupBy, volumeGroupBy]);

  useEffect(() => {
    load();
  }, [load]);

  const volumeItems = volume?.items || [];
  const byDoctor = revenue?.byDoctor || [];
  const bySpecialty = revenue?.bySpecialty || [];
  const totalVolume = volumeItems.reduce(
    (acc, row) => acc + (row.created || 0) + (row.confirmed || 0) + (row.cancelled || 0),
    0,
  );

  return (
    <div className="space-y-6" data-testid="admin-analytics-panel">
      <div className="flex flex-col lg:flex-row lg:items-end gap-3 lg:gap-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-0 sm:gap-x-3 flex-1 min-w-0">
          <Input
            label="From"
            type="date"
            name="analyticsFrom"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
          />
          <Input
            label="To"
            type="date"
            name="analyticsTo"
            value={to}
            onChange={(e) => setTo(e.target.value)}
          />
          <Select
            label="Revenue group"
            name="revenueGroupBy"
            value={revenueGroupBy}
            onChange={(e) => setRevenueGroupBy(e.target.value)}
          >
            <option value="doctor">By doctor</option>
            <option value="specialty">By specialty</option>
            <option value="">Total only</option>
          </Select>
          <Select
            label="Volume group"
            name="volumeGroupBy"
            value={volumeGroupBy}
            onChange={(e) => setVolumeGroupBy(e.target.value)}
          >
            <option value="day">By day</option>
            <option value="week">By week</option>
          </Select>
        </div>
        <Button
          type="button"
          className="w-full sm:w-auto shrink-0"
          leftIcon={<RefreshCw size={14} />}
          onClick={load}
          loading={loading}
        >
          Refresh
        </Button>
      </div>

      {loading && !revenue && !noShow && !volume ? (
        <Spinner />
      ) : (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 sm:gap-4">
            <MetricCard
              icon={<DollarSign size={18} className="text-primary" aria-hidden="true" />}
              label="Total revenue"
              value={formatMoney(revenue?.totalRevenue, revenue?.currency)}
              hint={revenue?.currency ? `Currency: ${revenue.currency}` : undefined}
            />
            <MetricCard
              icon={<Percent size={18} className="text-primary" aria-hidden="true" />}
              label="No-show rate"
              value={
                noShow == null
                  ? '—'
                  : `${Number(noShow.noShowRatePercent ?? 0).toFixed(1)}%`
              }
              hint={
                noShow
                  ? `${noShow.noShowCount ?? 0} no-shows / ${noShow.totalCount ?? 0} total`
                  : undefined
              }
            />
            <MetricCard
              icon={<CalendarRange size={18} className="text-primary" aria-hidden="true" />}
              label="Volume activity"
              value={String(totalVolume)}
              hint={`Grouped by ${volume?.groupBy || volumeGroupBy || 'day'}`}
            />
          </div>

          {noShow && (
            <div className="flex flex-wrap gap-2" aria-label="No-show breakdown">
              <Badge status="Confirmed">Completed: {noShow.completedCount ?? 0}</Badge>
              <Badge status="Scheduled">Confirmed: {noShow.confirmedCount ?? 0}</Badge>
              <Badge status="NoShow">No-show: {noShow.noShowCount ?? 0}</Badge>
              <Badge status="Pending">Total: {noShow.totalCount ?? 0}</Badge>
            </div>
          )}

          <section aria-labelledby="revenue-breakdown-heading">
            <h3 id="revenue-breakdown-heading" className="text-base font-medium text-text m-0 mb-3">
              Revenue breakdown
            </h3>
            {revenueGroupBy === 'doctor' && (
              byDoctor.length === 0 ? (
                <EmptyState message="No doctor revenue in this range." />
              ) : (
                <TableScroll label="Revenue by doctor">
                  <Table className="min-w-[28rem]">
                    <thead>
                      <tr>
                        <Th>Doctor</Th>
                        <Th>Revenue</Th>
                      </tr>
                    </thead>
                    <tbody>
                      {byDoctor.map((row) => (
                        <Tr key={row.doctorId ?? row.doctorName}>
                          <Td className="font-medium">{row.doctorName}</Td>
                          <Td className="whitespace-nowrap">
                            {formatMoney(row.revenue, revenue?.currency)}
                          </Td>
                        </Tr>
                      ))}
                    </tbody>
                  </Table>
                </TableScroll>
              )
            )}
            {revenueGroupBy === 'specialty' && (
              bySpecialty.length === 0 ? (
                <EmptyState message="No specialty revenue in this range." />
              ) : (
                <TableScroll label="Revenue by specialty">
                  <Table className="min-w-[28rem]">
                    <thead>
                      <tr>
                        <Th>Specialty</Th>
                        <Th>Revenue</Th>
                      </tr>
                    </thead>
                    <tbody>
                      {bySpecialty.map((row) => (
                        <Tr key={row.specialty}>
                          <Td className="font-medium">{row.specialty}</Td>
                          <Td className="whitespace-nowrap">
                            {formatMoney(row.revenue, revenue?.currency)}
                          </Td>
                        </Tr>
                      ))}
                    </tbody>
                  </Table>
                </TableScroll>
              )
            )}
            {!revenueGroupBy && (
              <EmptyState message="Select a revenue group to see breakdown, or use the total card above." />
            )}
          </section>

          <section aria-labelledby="volume-heading">
            <h3 id="volume-heading" className="text-base font-medium text-text m-0 mb-3">
              Appointment volume
            </h3>
            {volumeItems.length === 0 ? (
              <EmptyState message="No appointment volume in this range." />
            ) : (
              <TableScroll label="Appointment volume">
                <Table className="min-w-[32rem]">
                  <thead>
                    <tr>
                      <Th>Period</Th>
                      <Th>Created</Th>
                      <Th>Confirmed</Th>
                      <Th>Cancelled</Th>
                    </tr>
                  </thead>
                  <tbody>
                    {volumeItems.map((row) => (
                      <Tr key={row.period}>
                        <Td className="font-medium whitespace-nowrap">{row.period}</Td>
                        <Td>{row.created ?? 0}</Td>
                        <Td>{row.confirmed ?? 0}</Td>
                        <Td>{row.cancelled ?? 0}</Td>
                      </Tr>
                    ))}
                  </tbody>
                </Table>
              </TableScroll>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function MetricCard({ icon, label, value, hint }) {
  return (
    <Card className="min-w-0">
      <div className="flex items-start gap-3">
        <div className="mt-0.5 shrink-0 rounded-lg bg-surface p-2" aria-hidden="true">
          {icon}
        </div>
        <div className="min-w-0">
          <p className="text-xs uppercase tracking-wider text-text-muted m-0 mb-1">{label}</p>
          <p className="text-xl sm:text-2xl font-semibold text-text m-0 break-words" data-testid={`metric-${label}`}>
            {value}
          </p>
          {hint && <p className="text-xs text-text-muted m-0 mt-1 break-words">{hint}</p>}
        </div>
      </div>
    </Card>
  );
}
