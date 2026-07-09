import Navbar from './Navbar';

export default function Layout({ children }) {
  return (
    <>
      <Navbar />
      <main className="min-h-[calc(100vh-56px)] animate-[fadeIn_250ms_ease-out]">{children}</main>
    </>
  );
}
