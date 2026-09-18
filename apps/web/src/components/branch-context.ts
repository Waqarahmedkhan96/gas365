import { createContext, useContext } from 'react';
export const BranchContext = createContext('');
export const useBranch = () => useContext(BranchContext);
