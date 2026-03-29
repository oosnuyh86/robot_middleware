import Link from "next/link";

const sections = [
  {
    id: "01",
    title: "Companies",
    desc: "Manage client organizations and their composite painting operations",
    href: "/companies",
  },
  {
    id: "02",
    title: "Professionals",
    desc: "Configure operators with expertise profiles for recording sessions",
    href: "/professionals",
  },
  {
    id: "03",
    title: "Jobs",
    desc: "Define painting jobs with linked record sessions per composite part",
    href: "/jobs",
  },
  {
    id: "04",
    title: "Records",
    desc: "Full pipeline: scan, align, record, train, validate and execute",
    href: "/records",
  },
];

export default function Home() {
  return (
    <div className="page-container">
      <header className="page-header">
        <h1>Mission Control</h1>
        <p>Composite part painting -- recording, training & robot execution</p>
      </header>

      <div className="dashboard-grid">
        {sections.map((s) => (
          <Link key={s.id} href={s.href} className="card-link">
            <section className="card">
              <div className="card-id">[{s.id}]</div>
              <h2>{s.title}</h2>
              <p>{s.desc}</p>
              <span className="btn btn-ghost btn-sm">
                Open Module &rarr;
              </span>
            </section>
          </Link>
        ))}
      </div>
    </div>
  );
}
