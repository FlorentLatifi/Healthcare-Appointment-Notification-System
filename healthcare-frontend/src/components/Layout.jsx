import Navbar from './Navbar';

export default function Layout({ children }) {
  return (
    <>
      <a
        href="#main-content"
        className="absolute left-2 top-2 z-[100] -translate-y-[200%] rounded-md bg-white px-4 py-2.5 text-sm font-medium text-primary shadow-elevated focus:translate-y-0 focus:outline-2 focus:outline-offset-2 focus:outline-primary"
      >
        Skip to main content
      </a>
      <Navbar />
      <main
        id="main-content"
        tabIndex={-1}
        className="min-h-[calc(100vh-56px)] w-full max-w-[100vw] overflow-x-hidden animate-[fadeIn_250ms_ease-out] outline-none"
      >
        {children}
      </main>
    </>
  );
}
