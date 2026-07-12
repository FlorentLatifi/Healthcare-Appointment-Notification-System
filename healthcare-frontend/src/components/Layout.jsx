import Navbar from './Navbar';

export default function Layout({ children }) {
  return (
    <>
      <Navbar />
      <main className="min-h-[calc(100vh-56px)] w-full max-w-[100vw] overflow-x-hidden animate-[fadeIn_250ms_ease-out]">
        {children}
      </main>
    </>
  );
}
