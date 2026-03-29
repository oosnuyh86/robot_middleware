"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { get } from "@/lib/api";
import type { Company } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { Table } from "@/components/ui/Table";

export default function CompaniesPage() {
  const router = useRouter();
  const [companies, setCompanies] = useState<Company[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    get<Company[]>("/companies")
      .then(setCompanies)
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load companies"),
      )
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="page-container">
        <div className="loading"><div className="spinner" /></div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <div className="list-header">
        <h2>Companies</h2>
        <Link href="/companies/new">
          <Button>New Company</Button>
        </Link>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="list-container">
        <Table<Company>
          columns={[
            { header: "Name", accessor: "name" },
            {
              header: "Created",
              accessor: (c) => new Date(c.createdAt).toLocaleDateString(),
            },
          ]}
          data={companies}
          keyField="_id"
          onRowClick={(company) => router.push(`/companies/${company._id}`)}
        />
      </div>
    </div>
  );
}
