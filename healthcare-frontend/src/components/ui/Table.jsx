/**
 * Table, Th, Td — accessible table primitives with optional hover.
 * @param {{ hover?: boolean }} props — enable row hover via parent class "hover:bg-surface"
 */
export function Table({ className = '', children, ...props }) {
  return (
    <table className={`w-full border-collapse text-sm ${className}`} {...props}>
      {children}
    </table>
  );
}

export function Th({ className = '', children, ...props }) {
  return (
    <th
      className={`text-left px-4 py-3 border-b-2 border-border bg-surface font-semibold text-text-secondary text-xs uppercase tracking-wider ${className}`}
      {...props}
    >
      {children}
    </th>
  );
}

export function Td({ className = '', children, ...props }) {
  return (
    <td className={`px-4 py-3 border-b border-border-light text-text ${className}`} {...props}>
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
