import { useContext } from 'react';
import { AuthContext } from '../../context/AuthContext';
import ClassManagement from '../admin/ClassManagement';

/**
 * LecturerClasses — wraps the shared ClassManagement component.
 * Lecturers see the same UI but the API automatically filters to their assigned classes.
 */
export default function LecturerClasses() {
  const { user } = useContext(AuthContext);
  return <ClassManagement key={user?._id} />;
}
