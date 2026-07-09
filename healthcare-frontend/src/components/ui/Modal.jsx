import { useEffect, useRef, useCallback } from 'react';
import { X } from 'lucide-react';

/**
 * Modal — overlay dialog with title, body, footer, focus trap, and Escape-to-close.
 * @param {{ open: boolean, onClose: () => void, title?: string, footer?: ReactNode }} props
 */
export default function Modal({ open, onClose, title, footer, children }) {
  const overlayRef = useRef(null);
  const contentRef = useRef(null);

  /* Scroll lock */
  useEffect(() => {
    if (open) {
      document.body.style.overflow = 'hidden';
    }
    return () => { document.body.style.overflow = ''; };
  }, [open]);

  /* Escape key */
  const handleKeyDown = useCallback((e) => {
    if (e.key === 'Escape') onClose?.();
  }, [onClose]);

  useEffect(() => {
    if (open) {
      document.addEventListener('keydown', handleKeyDown);
      return () => document.removeEventListener('keydown', handleKeyDown);
    }
  }, [open, handleKeyDown]);

  /* Simple focus trap */
  useEffect(() => {
    if (!open) return;
    const el = contentRef.current;
    if (!el) return;
    const focusable = el.querySelectorAll(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
    );
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (first) first.focus();

    const trap = (e) => {
      if (e.key === 'Tab') {
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last?.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first?.focus();
        }
      }
    };
    el.addEventListener('keydown', trap);
    return () => el.removeEventListener('keydown', trap);
  }, [open]);

  if (!open) return null;

  return (
    <div
      ref={overlayRef}
      role="dialog"
      aria-modal="true"
      aria-label={title || 'Dialog'}
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/30 backdrop-blur-sm animate-[modalOverlayIn_200ms_ease-out]"
      onClick={(e) => { if (e.target === overlayRef.current) onClose?.(); }}
    >
      <div
        ref={contentRef}
        className="bg-white rounded-xl shadow-modal w-full max-w-lg max-h-[90vh] overflow-y-auto animate-[modalContentIn_200ms_ease-out]"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between px-6 pt-6 pb-0">
          <h3 className="text-lg font-semibold text-text m-0">{title}</h3>
          <button
            type="button"
            aria-label="Close"
            onClick={onClose}
            className="p-1 rounded-md text-text-muted hover:bg-surface hover:text-text transition-colors duration-150 cursor-pointer"
          >
            <X size={18} />
          </button>
        </div>
        <div className="px-6 py-5">{children}</div>
        {footer && (
          <div className="px-6 pb-6 pt-0 flex items-center justify-end gap-2">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}
