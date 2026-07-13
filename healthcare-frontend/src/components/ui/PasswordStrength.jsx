/**
 * Lightweight password strength meter aligned with backend RegisterRequestValidator rules.
 * @param {{ password: string }} props
 */
export default function PasswordStrength({ password = '' }) {
  const checks = [
    { id: 'len', label: 'At least 12 characters', ok: password.length >= 12 },
    { id: 'upper', label: 'One uppercase letter', ok: /[A-Z]/.test(password) },
    { id: 'lower', label: 'One lowercase letter', ok: /[a-z]/.test(password) },
    { id: 'num', label: 'One number', ok: /[0-9]/.test(password) },
    { id: 'special', label: 'One special character', ok: /[^a-zA-Z0-9]/.test(password) },
  ];

  const score = checks.filter((c) => c.ok).length;
  const pct = (score / checks.length) * 100;

  const barColor =
    score <= 2 ? 'bg-status-cancelled-text' :
    score <= 4 ? 'bg-status-pending-text' :
    'bg-status-completed-text';

  const label =
    score === 0 ? '' :
    score <= 2 ? 'Weak' :
    score <= 4 ? 'Fair' :
    'Strong';

  if (!password) return null;

  return (
    <div className="mb-4 -mt-2" aria-live="polite" role="status">
      <div className="flex items-center justify-between mb-1 gap-2">
        <span className="text-xs text-text-muted">Password strength</span>
        {label && (
          <span className="text-xs font-medium text-text-secondary">
            {label}
            <span className="sr-only">
              {`, ${score} of ${checks.length} requirements met`}
            </span>
          </span>
        )}
      </div>
      <div
        className="h-1.5 w-full rounded-full bg-surface overflow-hidden mb-2"
        role="progressbar"
        aria-valuenow={score}
        aria-valuemin={0}
        aria-valuemax={checks.length}
        aria-label="Password strength progress"
      >
        <div
          className={`h-full rounded-full transition-all duration-200 ${barColor}`}
          style={{ width: `${pct}%` }}
        />
      </div>
      <ul className="grid grid-cols-1 gap-0.5 m-0 p-0 list-none">
        {checks.map((c) => (
          <li
            key={c.id}
            className={`text-xs flex items-center gap-1.5 ${c.ok ? 'text-status-completed-text' : 'text-text-muted'}`}
          >
            <span aria-hidden="true">{c.ok ? '✓' : '○'}</span>
            {c.label}
          </li>
        ))}
      </ul>
    </div>
  );
}
