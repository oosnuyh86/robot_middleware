"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { get } from "@/lib/api";
import type { Professional } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { Table } from "@/components/ui/Table";

export default function ProfessionalsPage() {
  const router = useRouter();
  const [professionals, setProfessionals] = useState<Professional[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    get<Professional[]>("/professionals")
      .then(setProfessionals)
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load professionals"),
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
        <h2>Professionals</h2>
        <Link href="/professionals/new">
          <Button>New Professional</Button>
        </Link>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="list-container">
        <Table<Professional>
          columns={[
            { header: "Name", accessor: "name" },
            { header: "Profile", accessor: "profile_summary" },
            {
              header: "Created",
              accessor: (p) => new Date(p.createdAt).toLocaleDateString(),
            },
          ]}
          data={professionals}
          keyField="_id"
          onRowClick={(p) => router.push(`/professionals/${p._id}`)}
        />
      </div>
    </div>
  );
}
