/**
 * PageHeader — consistent page title bar with optional subtitle and action slot.
 * @param {{ title: string, subtitle?: string, actions?: ReactNode, className?: string }} props
 */
export default function PageHeader({ title, subtitle, actions, className = '' }) {
  return (
    <div className={`flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-4 sm:mb-6 ${className}`}>
      <div className="min-w-0">
        <h1 className="text-xl sm:text-2xl font-semibold text-text tracking-tight m-0 break-words">{title}</h1>
        {subtitle && <p className="text-sm text-text-muted mt-1 m-0 break-words">{subtitle}</p>}
      </div>
      {actions && (
        <div className="flex flex-wrap items-center gap-2 shrink-0 w-full sm:w-auto">
          {actions}
        </div>
      )}
    </div>
  );
}
