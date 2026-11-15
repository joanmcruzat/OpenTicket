import { UserRole } from '../api/models';
import { DemoRole } from './auth-options';

export interface DemoRoleInfo {
  key: DemoRole;
  role: UserRole;
  label: string;
  person: string;
  icon: string;
  summary: string;
}

/** The three demo personas, in the order the sign-in page offers them. Names match the API's demo seed. */
export const DEMO_ROLES: readonly DemoRoleInfo[] = [
  {
    key: 'developer',
    role: 'Agent',
    label: 'Developer',
    person: 'Alex Rivera',
    icon: 'code',
    summary: 'Pick up work, drag tickets across the board and close them out.',
  },
  {
    key: 'reporter',
    role: 'Customer',
    label: 'Reporter',
    person: 'Jordan Kim',
    icon: 'campaign',
    summary: 'Report bugs and ideas, then follow their progress.',
  },
  {
    key: 'admin',
    role: 'Admin',
    label: 'Admin',
    person: 'Morgan Lee',
    icon: 'admin_panel_settings',
    summary: 'Assign work, move any ticket and manage users and categories.',
  },
];
