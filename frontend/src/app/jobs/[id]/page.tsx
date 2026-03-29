"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { get, del } from "@/lib/api";
import type { Job } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";

export default function JobDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [job, setJob] = useState<Job | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showDelete, setShowDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (!id) return;
    get<Job>(`/jobs/${id}`)
      .then(setJob)
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load job"),
      )
      .finally(() => setLoading(false));
  }, [id]);

  async function handleDelete() {
    setDeleting(true);
    try {
      await del(`/jobs/${id}`);
      router.push("/jobs");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete job");
      setDeleting(false);
      setShowDelete(false);
    }
  }

  if (loading) {
    return (
      <div className="page-container">
        <div className="loading"><div className="spinner" /></div>
      </div>
    );
  }

  if (!job) {
    return (
      <div className="page-container">
        <div className="error-message">{error || "Job not found"}</div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <div className="detail-header">
        <h1>Job Details</h1>
        <div className="detail-header-actions">
          <Button onClick={() => router.push(`/jobs/${id}/edit`)}>Edit</Button>
          <Button variant="danger" onClick={() => setShowDelete(true)}>
            Delete
          </Button>
        </div>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="detail-container">
        <div className="detail-group">
          <label>Job ID</label>
          <p>{job._id}</p>
        </div>
        <div className="detail-group">
          <label>Description</label>
          <p>{job.description}</p>
        </div>
        <div className="detail-group">
          <label>Company ID</label>
          <p>{job.company_id}</p>
        </div>
        <div className="detail-group">
          <label>Associated Records</label>
          {job.record_ids.length === 0 ? (
            <p>No records</p>
          ) : (
            <div className="chip-list">
              {job.record_ids.map((rid) => (
                <Link key={rid} href={`/records/${rid}`} className="chip">
                  {rid.slice(-8)}
                </Link>
              ))}
            </div>
          )}
        </div>
        <div className="detail-group">
          <label>Created</label>
          <p>{new Date(job.createdAt).toLocaleString()}</p>
        </div>
        <div className="detail-group">
          <label>Updated</label>
          <p>{new Date(job.updatedAt).toLocaleString()}</p>
        </div>

        <Button variant="ghost" onClick={() => router.push("/jobs")}>
          Back to Jobs
        </Button>
      </div>

      <Modal
        open={showDelete}
        title="Delete Job"
        onClose={() => setShowDelete(false)}
        onConfirm={handleDelete}
        confirmLabel="Delete"
        confirmVariant="danger"
        loading={deleting}
      >
        <p>Are you sure you want to delete this job? This action cannot be undone.</p>
      </Modal>
    </div>
  );
}
