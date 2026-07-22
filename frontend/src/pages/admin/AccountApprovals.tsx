import { useEffect, useState } from 'react';
import AccountApprovalBoard from '../../components/admin/AccountApprovalBoard';
import {
  adminApprovalApi,
  getAdminApprovalErrorMessage,
} from '../../api/adminApprovalApi';
import type {
  AccountApprovalDecision,
  AccountApprovalRequest,
} from '../../types/accountApproval';
import {
  applyApprovalDecision,
  registrationToApprovalRequest,
} from '../../utils/accountApproval';

export default function AccountApprovals() {
  const [requests, setRequests] = useState<AccountApprovalRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    const loadRegistrations = async () => {
      setLoading(true);
      setLoadError(null);
      try {
        const response = await adminApprovalApi.getPending();
        const approvalRequests = (response.data || [])
          .map((record) => registrationToApprovalRequest(record))
          .filter((request: AccountApprovalRequest | null): request is AccountApprovalRequest => Boolean(request));
        if (active) setRequests(approvalRequests);
      } catch (error) {
        if (active) {
          setRequests([]);
          setLoadError(getAdminApprovalErrorMessage(error));
        }
      } finally {
        if (active) setLoading(false);
      }
    };

    void loadRegistrations();
    return () => { active = false; };
  }, []);

  const handleDecision = async (decision: AccountApprovalDecision) => {
    try {
      if (decision.status === 'APPROVED') {
        await adminApprovalApi.approve(decision.requestId);
      } else {
        await adminApprovalApi.reject(decision.requestId);
      }
    } catch (error) {
      throw new Error(getAdminApprovalErrorMessage(error));
    }

    setRequests((current) => applyApprovalDecision(current, decision));
  };

  return (
    <AccountApprovalBoard
      requests={requests}
      loading={loading}
      loadError={loadError}
      onDecision={handleDecision}
    />
  );
}
