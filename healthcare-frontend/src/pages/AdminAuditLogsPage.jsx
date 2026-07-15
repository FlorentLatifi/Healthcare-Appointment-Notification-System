import { PageHeader } from '../components/ui';
import AdminAuditLogsPanel from './admin/AdminAuditLogsPanel';

/**
 * Admin-only immutable audit log browser.
 * Data: GET /AuditLogs (paginated + filters)
 */
export default function AdminAuditLogsPage() {
  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <PageHeader
        title="Audit logs"
        subtitle="Search and review append-only security and PHI access events."
      />
      <AdminAuditLogsPanel />
    </div>
  );
}
