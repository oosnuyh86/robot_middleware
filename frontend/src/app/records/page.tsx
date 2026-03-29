"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { get } from "@/lib/api";
import type { RecordEntry } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { Table } from "@/components/ui/Table";
import { StatusBadge } from "@/components/ui/StatusBadge";

export default function RecordsPage() {
  const router = useRouter();
  const [records, setRecords] = useState<RecordEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    get<RecordEntry[]>("/records")
      .then(setRecords)
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load records"),
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
        <h2>Records</h2>
        <Link href="/records/new">
          <Button>New Record</Button>
        </Link>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="list-container">
        <Table<RecordEntry>
          columns={[
            { header: "Subject Type", accessor: (r) => r.subject.type },
            {
              header: "State",
              accessor: (r) => <StatusBadge state={r.state} />,
            },
            {
              header: "Sensors",
              accessor: (r) => r.sensors_used.join(", "),
            },
            {
              header: "Created",
              accessor: (r) => new Date(r.createdAt).toLocaleDateString(),
            },
          ]}
          data={records}
          keyField="_id"
          onRowClick={(record) => router.push(`/records/${record._id}`)}
        />
      </div>
    </div>
  );
}
