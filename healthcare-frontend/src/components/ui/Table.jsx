/**
 * Table primitives with mobile-friendly horizontal scroll wrapper.
 * Use <TableScroll> around wide tables so 375px viewports never force page overflow.
 */

/** Scroll container for wide tables — prevents page-level horizontal scroll. */
export function TableScroll({ className = '', children }) {
  return (
    <div
      className={`w-full max-w-full overflow-x-auto overscroll-x-contain -mx-0 rounded-xl border border-border-light bg-white shadow-card ${className}`}
    >
      {children}
    </div>
  );
}

/**
 * @param {{ hover?: boolean }} props
 */
export function Table({ className = '', children, ...props }) {
  return (
    <table
      className={`w-full min-w-[36rem] border-collapse text-sm ${className}`}
      {...props}
    >
      {children}
    </table>
  );
}

export function Th({ className = '', children, ...props }) {
  return (
    <th
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
      className={`px-3 py-2.5 sm:px-4 sm:py-3 border-b border-border-light text-text ${className}`}
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
