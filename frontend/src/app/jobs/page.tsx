"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { get } from "@/lib/api";
import type { Job } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { Table } from "@/components/ui/Table";

export default function JobsPage() {
  const router = useRouter();
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    get<Job[]>("/jobs")
      .then(setJobs)
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load jobs"),
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
        <h2>Jobs</h2>
        <Link href="/jobs/new">
          <Button>New Job</Button>
        </Link>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="list-container">
        <Table<Job>
          columns={[
            { header: "Description", accessor: "description" },
            {
              header: "Records",
              accessor: (j) => `${j.record_ids.length} record(s)`,
            },
            {
              header: "Created",
              accessor: (j) => new Date(j.createdAt).toLocaleDateString(),
            },
          ]}
          data={jobs}
          keyField="_id"
          onRowClick={(job) => router.push(`/jobs/${job._id}`)}
        />
      </div>
    </div>
  );
}
