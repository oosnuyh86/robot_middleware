import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "Robot Middleware",
  description: "Composite part painting -- recording, training & execution",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <div className="app-container">
          <nav className="sidebar">
            <div className="sidebar-header">
              <h1>Robot<br />Middleware</h1>
              <div className="sidebar-subtitle">Control System v1.0</div>
            </div>
            <ul className="nav-menu">
              <li>
                <Link href="/">
                  <span className="nav-icon">//</span>
                  Dashboard
                </Link>
              </li>
              <li>
                <Link href="/companies">
                  <span className="nav-icon">01</span>
                  Companies
                </Link>
              </li>
              <li>
                <Link href="/professionals">
                  <span className="nav-icon">02</span>
                  Professionals
                </Link>
              </li>
              <li>
                <Link href="/jobs">
                  <span className="nav-icon">03</span>
                  Jobs
                </Link>
              </li>
              <li>
                <Link href="/records">
                  <span className="nav-icon">04</span>
                  Records
                </Link>
              </li>
            </ul>
            <div className="sidebar-footer">
              <div className="status-online">SYSTEM ONLINE</div>
            </div>
          </nav>
          <main className="main-content">{children}</main>
        </div>
      </body>
    </html>
  );
}
