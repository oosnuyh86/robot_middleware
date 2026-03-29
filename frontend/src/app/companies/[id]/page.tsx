"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { get, del } from "@/lib/api";
import type { Company } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";

export default function CompanyDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [company, setCompany] = useState<Company | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showDelete, setShowDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (!id) return;
    get<Company>(`/companies/${id}`)
      .then(setCompany)
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load company"),
      )
      .finally(() => setLoading(false));
  }, [id]);

  async function handleDelete() {
    setDeleting(true);
    try {
      await del(`/companies/${id}`);
      router.push("/companies");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete company");
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

  if (!company) {
    return (
      <div className="page-container">
        <div className="error-message">{error || "Company not found"}</div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <div className="detail-header">
        <h1>{company.name}</h1>
        <div className="detail-header-actions">
          <Button onClick={() => router.push(`/companies/${id}/edit`)}>
            Edit
          </Button>
          <Button variant="danger" onClick={() => setShowDelete(true)}>
            Delete
          </Button>
        </div>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="detail-container">
        <div className="detail-group">
          <label>Company ID</label>
          <p>{company._id}</p>
        </div>
        <div className="detail-group">
          <label>Created</label>
          <p>{new Date(company.createdAt).toLocaleString()}</p>
        </div>
        <div className="detail-group">
          <label>Updated</label>
          <p>{new Date(company.updatedAt).toLocaleString()}</p>
        </div>

        <Button variant="ghost" onClick={() => router.push("/companies")}>
          Back to Companies
        </Button>
      </div>

      <Modal
        open={showDelete}
        title="Delete Company"
        onClose={() => setShowDelete(false)}
        onConfirm={handleDelete}
        confirmLabel="Delete"
        confirmVariant="danger"
        loading={deleting}
      >
        <p>
          Are you sure you want to delete <strong>{company.name}</strong>? This
          action cannot be undone.
        </p>
      </Modal>
    </div>
  );
}
