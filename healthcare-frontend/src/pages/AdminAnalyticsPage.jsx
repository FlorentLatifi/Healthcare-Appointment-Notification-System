import { PageHeader } from '../components/ui';
import AdminAnalyticsPanel from './admin/AdminAnalyticsPanel';

/**
 * Admin-only analytics dashboard.
 * Data: GET /Analytics/revenue, /Analytics/no-show-rate, /Analytics/volume
 */
export default function AdminAnalyticsPage() {
  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <PageHeader
        title="Analytics"
        subtitle="Revenue, no-show rate, and appointment volume for the selected date range."
      />
      <AdminAnalyticsPanel />
    </div>
  );
}
