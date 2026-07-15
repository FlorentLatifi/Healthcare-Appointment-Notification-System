/**
 * Table primitives with mobile-friendly horizontal scroll wrapper.
 * Use <TableScroll> around wide tables so 375px viewports never force page overflow.
 */

/** Scroll container for wide tables — prevents page-level horizontal scroll at 375px. */
export function TableScroll({ className = '', children, label = 'Data table' }) {
  return (
    <div
      className={`w-full max-w-full overflow-x-auto overscroll-x-contain rounded-xl border border-border-light bg-white shadow-card [-webkit-overflow-scrolling:touch] ${className}`}
      role="region"
      aria-label={label}
      tabIndex={0}
    >
      {children}
    </div>
  );
}

/**
 * Wide tables scroll inside TableScroll — never expand the page past the viewport.
 * @param {{ className?: string }} props
 */
export function Table({ className = '', children, ...props }) {
  return (
    <table
      className={`w-full min-w-[min(100%,20rem)] sm:min-w-[36rem] border-collapse text-sm ${className}`}
      {...props}
    >
      {children}
    </table>
  );
}

export function Th({ className = '', children, scope = 'col', ...props }) {
  return (
    <th
      scope={scope}
      className={`text-left px-3 py-2.5 sm:px-4 sm:py-3 border-b-2 border-border bg-surface font-semibold text-text-secondary text-xs uppercase tracking-wider whitespace-nowrap ${className}`}
      {...props}
    >
      {children}
    </th>
  );
}

export function Td({ className = '', children, ...props }) {
  return (
    <td
      className={`px-3 py-2.5 sm:px-4 sm:py-3 border-b border-border-light text-text break-words ${className}`}
      {...props}
    >
      {children}
    </td>
  );
}

export function Tr({ className = '', children, ...props }) {
  return (
    <tr className={`transition-colors duration-150 hover:bg-surface ${className}`} {...props}>
      {children}
    </tr>
  );
}
