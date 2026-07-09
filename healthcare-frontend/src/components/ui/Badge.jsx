const statusStyles = {
  Scheduled: 'bg-status-scheduled-bg text-status-scheduled-text',
  Confirmed: 'bg-status-confirmed-bg text-status-confirmed-text',
  Completed: 'bg-status-completed-bg text-status-completed-text',
  Cancelled: 'bg-status-cancelled-bg text-status-cancelled-text',
  NoShow: 'bg-status-noshow-bg text-status-noshow-text',
  InProgress: 'bg-status-inprogress-bg text-status-inprogress-text',
};

export default function Badge({ status, children, className = '' }) {
  const style = statusStyles[status] || 'bg-surface text-text-muted';
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${style} ${className}`}>
      {children || status}
    </span>
  );
}
