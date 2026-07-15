import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { loadStripe } from '@stripe/stripe-js';
import { Elements, PaymentElement, useStripe, useElements } from '@stripe/react-stripe-js';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { Button, Card, Spinner, PageHeader } from '../components/ui';
import { ArrowLeft, CreditCard, AlertCircle } from 'lucide-react';

/** Playwright / CI: skip real Stripe Elements and card network. Never enable in production builds. */
const E2E_MOCK_STRIPE = import.meta.env.VITE_E2E_MOCK_STRIPE === 'true';

let stripePromise;

function getStripe() {
  if (E2E_MOCK_STRIPE) return null;
  if (!stripePromise) {
    const key = import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY;
    if (!key) {
      console.error('VITE_STRIPE_PUBLISHABLE_KEY is not set');
      return null;
    }
    stripePromise = loadStripe(key);
  }
  return stripePromise;
}

/**
 * E2E-only payment control: posts the same /Payments/process payload the real Stripe path uses,
 * with a deterministic fake PaymentIntent id (no external Stripe calls).
 */
function MockPaymentForm({ appointmentId, appointment, onSuccess }) {
  const [error, setError] = useState(null);
  const [processing, setProcessing] = useState(false);

  const payLabel = appointment?.consultationFeeAmount != null
    ? `Pay $${Number(appointment.consultationFeeAmount).toFixed(2)}`
    : 'Pay now';

  const handleSubmit = async (e) => {
    e.preventDefault();
    setProcessing(true);
    setError(null);
    try {
      const { data } = await apiClient.post('/Payments/process', {
        appointmentId,
        paymentIntentId: `pi_e2e_mock_${appointmentId}`,
      });
      if (data.success) {
        toast.success('Payment successful! Your appointment is confirmed.');
        onSuccess();
      } else {
        setError(data.message || 'Payment reconciliation failed. Contact support.');
      }
    } catch (err) {
      setError(
        err.response?.data?.errors?.join('. ')
        || err.response?.data?.message
        || 'Failed to process payment. Contact support.',
      );
    } finally {
      setProcessing(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} aria-label="Payment form" data-testid="e2e-mock-payment-form" className="min-w-0">
      <div className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light mb-4 min-w-0">
        <div className="flex items-center gap-2 mb-2">
          <CreditCard size={18} className="text-primary shrink-0" aria-hidden="true" />
          <h3 className="text-sm font-semibold text-text m-0">Test payment (E2E mock)</h3>
        </div>
        <p className="text-xs text-text-muted m-0">
          Stripe is mocked for automated tests. No real card is charged.
        </p>
      </div>
      {error && (
        <div
          className="flex items-start gap-2 bg-status-cancelled-bg text-status-cancelled-text rounded-lg p-3 mb-4 text-sm break-words"
          role="alert"
        >
          <AlertCircle size={16} className="shrink-0 mt-0.5" aria-hidden="true" />
          <span>{error}</span>
        </div>
      )}
      <Button
        type="submit"
        loading={processing}
        className="w-full"
        size="lg"
        data-testid="e2e-mock-pay-button"
      >
        {processing ? 'Processing Payment...' : payLabel}
      </Button>
    </form>
  );
}

function PaymentForm({ appointmentId, appointment, onSuccess }) {
  const stripe = useStripe();
  const elements = useElements();
  const [error, setError] = useState(null);
  const [processing, setProcessing] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!stripe || !elements) return;

    setProcessing(true);
    setError(null);

    const { error: submitError, paymentIntent } = await stripe.confirmPayment({
      elements,
      redirect: 'if_required',
    });

    if (submitError) {
      setError(submitError.message || 'Payment failed. Please try again.');
      setProcessing(false);
      return;
    }

    if (paymentIntent && paymentIntent.status === 'succeeded') {
      try {
        const { data } = await apiClient.post('/Payments/process', {
          appointmentId,
          paymentIntentId: paymentIntent.id,
        });
        if (data.success) {
          toast.success('Payment successful! Your appointment is confirmed.');
          onSuccess();
        } else {
          setError(data.message || 'Payment reconciliation failed. Contact support.');
        }
      } catch (err) {
        setError(
          err.response?.data?.errors?.join('. ') ||
          err.response?.data?.message ||
          'Failed to process payment. Contact support.'
        );
      }
    } else if (paymentIntent && paymentIntent.status === 'requires_action') {
      setError('Additional authentication is required. Please try again.');
    } else {
      setError('Payment did not complete. Please try again.');
    }
    setProcessing(false);
  };

  const payLabel = appointment?.consultationFeeAmount != null
    ? `Pay $${Number(appointment.consultationFeeAmount).toFixed(2)}`
    : 'Pay now';

  return (
    <form onSubmit={handleSubmit} aria-label="Payment form" className="min-w-0">
      <div className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light mb-4 min-w-0">
        <div className="flex items-center gap-2 mb-4">
          <CreditCard size={18} className="text-primary shrink-0" aria-hidden="true" />
          <h3 className="text-sm font-semibold text-text">Card Details</h3>
        </div>
        <div className="min-w-0 overflow-x-auto overscroll-x-contain">
          <PaymentElement options={{ layout: 'tabs' }} />
        </div>
      </div>

      {error && (
        <div
          className="flex items-start gap-2 bg-status-cancelled-bg text-status-cancelled-text rounded-lg p-3 mb-4 text-sm break-words"
          role="alert"
        >
          <AlertCircle size={16} className="shrink-0 mt-0.5" aria-hidden="true" />
          <span>{error}</span>
        </div>
      )}

      <Button
        type="submit"
        disabled={!stripe || processing}
        loading={processing}
        className="w-full"
        size="lg"
      >
        {processing ? 'Processing Payment...' : payLabel}
      </Button>
    </form>
  );
}

export default function StripePaymentPage() {
  const { appointmentId } = useParams();
  const navigate = useNavigate();
  const [appointment, setAppointment] = useState(null);
  const [loading, setLoading] = useState(true);
  const [clientSecret, setClientSecret] = useState(null);
  const [intentError, setIntentError] = useState(null);
  const [stripeInstance, setStripeInstance] = useState(null);

  useEffect(() => {
    (async () => {
      try {
        const stripe = getStripe();
        setStripeInstance(stripe);
      } catch { /* handled below */ }
    })();
  }, []);

  useEffect(() => {
    (async () => {
      setLoading(true);
      try {
        const { data: apptData } = await apiClient.get(`/Appointments/${appointmentId}`);
        if (!apptData.success) {
          toast.error('Appointment not found');
          navigate('/my-appointments');
          return;
        }
        setAppointment(apptData.data);

        // E2E: skip Stripe PaymentIntent creation; still load appointment for the money UI.
        if (E2E_MOCK_STRIPE) {
          setClientSecret('e2e_mock_client_secret');
          return;
        }

        const { data: intentData } = await apiClient.post('/Payments/create-intent', {
          appointmentId: Number(appointmentId),
        });
        if (!intentData.success) {
          if (intentData.message?.includes('already been processed')) {
            toast.success('This appointment is already paid.');
            navigate('/my-appointments');
            return;
          }
          setIntentError(intentData.errors?.join('. ') || intentData.message || 'Failed to initialize payment');
          setLoading(false);
          return;
        }
        setClientSecret(intentData.data.clientSecret);
      } catch (err) {
        setIntentError(
          err.response?.data?.errors?.join('. ') ||
          err.response?.data?.message ||
          'Failed to load payment details'
        );
      } finally {
        setLoading(false);
      }
    })();
  }, [appointmentId, navigate]);

  const handleSuccess = () => {
    navigate('/my-appointments');
  };

  if (loading) {
    return (
      <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
        <div className="flex justify-center py-8" role="status" aria-label="Loading payment">
          <Spinner />
        </div>
      </div>
    );
  }

  const amount = appointment?.consultationFeeAmount;
  const currency = appointment?.consultationFeeCurrency || 'USD';

  return (
    <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <Button
        variant="ghost"
        size="sm"
        className="mb-4 sm:mb-6 -ml-1 sm:-ml-2"
        onClick={() => navigate('/my-appointments')}
        leftIcon={<ArrowLeft size={14} />}
        aria-label="Back to my appointments"
      >
        <span className="hidden sm:inline">Back to Appointments</span>
        <span className="sm:hidden">Back</span>
      </Button>

      <PageHeader title="Complete Payment" />

      {appointment && (
        <Card className="mb-4 sm:mb-6" aria-label="Appointment summary">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 min-w-0">
            <div className="min-w-0">
              <p className="text-sm font-medium text-text break-all">{appointment.referenceCode}</p>
              <p className="text-xs text-text-muted mt-0.5 break-words">
                Dr. {appointment.doctor?.fullName} — {appointment.scheduledDate}
              </p>
            </div>
            <span className="text-lg font-bold text-primary shrink-0 tabular-nums">
              {currency} {amount?.toFixed(2)}
            </span>
          </div>
        </Card>
      )}

      {intentError ? (
        <div className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light text-center min-w-0">
          <AlertCircle size={32} className="mx-auto mb-3 text-status-cancelled-text" aria-hidden="true" />
          <p className="text-sm text-text-muted mb-4 break-words" role="alert">{intentError}</p>
          <Button variant="primary" className="w-full sm:w-auto" onClick={() => window.location.reload()}>
            Try Again
          </Button>
        </div>
      ) : E2E_MOCK_STRIPE && appointment ? (
        <MockPaymentForm
          appointmentId={Number(appointmentId)}
          appointment={appointment}
          onSuccess={handleSuccess}
        />
      ) : clientSecret && stripeInstance ? (
        <Elements stripe={stripeInstance} options={{ clientSecret }}>
          <PaymentForm
            appointmentId={Number(appointmentId)}
            appointment={appointment}
            onSuccess={handleSuccess}
          />
        </Elements>
      ) : (
        <div className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light text-center" role="status">
          <p className="text-sm text-text-muted">Loading payment form...</p>
        </div>
      )}
    </div>
  );
}
