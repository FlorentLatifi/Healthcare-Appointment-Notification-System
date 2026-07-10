import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { loadStripe } from '@stripe/stripe-js';
import { Elements, PaymentElement, useStripe, useElements } from '@stripe/react-stripe-js';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { Button, Card, Spinner, PageHeader } from '../components/ui';
import { ArrowLeft, CreditCard, AlertCircle } from 'lucide-react';

let stripePromise;

function getStripe() {
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

  return (
    <form onSubmit={handleSubmit}>
      <div className="bg-white rounded-xl shadow-card p-6 border border-border-light mb-4">
        <div className="flex items-center gap-2 mb-4">
          <CreditCard size={18} className="text-primary" />
          <h3 className="text-sm font-semibold text-text">Card Details</h3>
        </div>
        <PaymentElement />
      </div>

      {error && (
        <div className="flex items-start gap-2 bg-status-cancelled-bg text-status-cancelled-text rounded-lg p-3 mb-4 text-sm">
          <AlertCircle size={16} className="shrink-0 mt-0.5" />
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
        {processing ? 'Processing Payment...' : `Pay $${appointment?.consultationFeeAmount?.toFixed(2) || ''}`}
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
      <div className="max-w-lg mx-auto px-4 py-12">
        <div className="flex justify-center py-8"><Spinner /></div>
      </div>
    );
  }

  const amount = appointment?.consultationFeeAmount;
  const currency = appointment?.consultationFeeCurrency || 'USD';

  return (
    <div className="max-w-lg mx-auto px-4 py-12">
      <Button
        variant="ghost"
        size="sm"
        className="mb-6 -ml-2"
        onClick={() => navigate('/my-appointments')}
      >
        <ArrowLeft size={14} />
        Back to Appointments
      </Button>

      <PageHeader title="Complete Payment" />

      {appointment && (
        <Card className="mb-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-text">{appointment.referenceCode}</p>
              <p className="text-xs text-text-muted mt-0.5">
                Dr. {appointment.doctor?.fullName} — {appointment.scheduledDate}
              </p>
            </div>
            <span className="text-lg font-bold text-primary">
              {currency} {amount?.toFixed(2)}
            </span>
          </div>
        </Card>
      )}

      {intentError ? (
        <div className="bg-white rounded-xl shadow-card p-6 border border-border-light text-center">
          <AlertCircle size={32} className="mx-auto mb-3 text-status-cancelled-text" />
          <p className="text-sm text-text-muted mb-4">{intentError}</p>
          <Button variant="primary" onClick={() => window.location.reload()}>
            Try Again
          </Button>
        </div>
      ) : clientSecret && stripeInstance ? (
        <Elements stripe={stripeInstance} options={{ clientSecret }}>
          <PaymentForm
            appointmentId={Number(appointmentId)}
            appointment={appointment}
            onSuccess={handleSuccess}
          />
        </Elements>
      ) : (
        <div className="bg-white rounded-xl shadow-card p-6 border border-border-light text-center">
          <p className="text-sm text-text-muted">Loading payment form...</p>
        </div>
      )}
    </div>
  );
}
