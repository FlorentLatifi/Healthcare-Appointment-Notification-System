import { useEffect, useRef, useCallback, useId } from 'react';
import { X } from 'lucide-react';

/**
 * Modal — overlay dialog with title, body, footer, focus trap, restore-focus, and Escape-to-close.
 * Mobile: bottom sheet style; desktop: centered dialog. Close control is ≥44×44px.
 * @param {{ open: boolean, onClose: () => void, title?: string, footer?: ReactNode, initialFocusRef?: React.RefObject }} props
 */
export default function Modal({ open, onClose, title, footer, children, initialFocusRef }) {
  const overlayRef = useRef(null);
  const contentRef = useRef(null);
  const previouslyFocused = useRef(null);
  const titleId = useId();

  /* Scroll lock + remember focus to restore on close */
  useEffect(() => {
    if (open) {
      previouslyFocused.current = document.activeElement;
      document.body.style.overflow = 'hidden';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [open]);

  useEffect(() => {
    if (!open && previouslyFocused.current instanceof HTMLElement) {
      previouslyFocused.current.focus?.();
      previouslyFocused.current = null;
    }
  }, [open]);

  /* Escape key */
  const handleKeyDown = useCallback((e) => {
    if (e.key === 'Escape') {
      e.stopPropagation();
      onClose?.();
    }
  }, [onClose]);

  useEffect(() => {
    if (open) {
      document.addEventListener('keydown', handleKeyDown);
      return () => document.removeEventListener('keydown', handleKeyDown);
    }
  }, [open, handleKeyDown]);

  /* Focus trap + initial focus */
  useEffect(() => {
    if (!open) return;
    const el = contentRef.current;
    if (!el) return;

    const getFocusable = () =>
      Array.from(
        el.querySelectorAll(
          'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
        ),
      ).filter((node) => {
        // Skip elements that cannot receive focus (hidden / display:none)
        if (!(node instanceof HTMLElement)) return false;
        if (node.getAttribute('aria-hidden') === 'true') return false;
        const style = window.getComputedStyle(node);
        return style.visibility !== 'hidden' && style.display !== 'none';
      });

    const focusable = getFocusable();
    const preferred = initialFocusRef?.current;
    if (preferred && el.contains(preferred)) {
      preferred.focus();
    } else if (focusable[0]) {
      focusable[0].focus();
    } else {
      el.setAttribute('tabindex', '-1');
      el.focus();
    }

    const trap = (e) => {
      if (e.key !== 'Tab') return;
      const nodes = getFocusable();
      if (!nodes.length) {
        e.preventDefault();
        el.focus();
        return;
      }
      const first = nodes[0];
      const last = nodes[nodes.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };
    el.addEventListener('keydown', trap);
    return () => el.removeEventListener('keydown', trap);
  }, [open, initialFocusRef]);

  if (!open) return null;

  return (
    <div
      ref={overlayRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby={title ? titleId : undefined}
      aria-label={title ? undefined : 'Dialog'}
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-0 sm:p-4 bg-black/30 backdrop-blur-sm animate-[modalOverlayIn_200ms_ease-out]"
      onClick={(e) => { if (e.target === overlayRef.current) onClose?.(); }}
    >
      <div
        ref={contentRef}
        className="bg-white rounded-t-2xl sm:rounded-xl shadow-modal w-full sm:max-w-lg max-h-[92vh] sm:max-h-[90vh] overflow-y-auto overscroll-contain animate-[modalContentIn_200ms_ease-out] outline-none"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between gap-3 px-4 sm:px-6 pt-4 sm:pt-6 pb-0 sticky top-0 bg-white z-10">
          <h3
            id={titleId}
            className="text-base sm:text-lg font-semibold text-text m-0 pr-2 break-words min-w-0"
          >
            {title}
          </h3>
          <button
            type="button"
            aria-label="Close dialog"
            onClick={onClose}
            className="inline-flex items-center justify-center min-w-11 min-h-11 p-2 rounded-md text-text-muted hover:bg-surface hover:text-text transition-colors duration-150 cursor-pointer shrink-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          >
            <X size={18} aria-hidden="true" />
          </button>
        </div>
        <div className="px-4 sm:px-6 py-4 sm:py-5 min-w-0">{children}</div>
        {footer && (
          <div className="px-4 sm:px-6 pb-4 sm:pb-6 pt-0 flex flex-col-reverse sm:flex-row items-stretch sm:items-center justify-end gap-2 sm:gap-3">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}
