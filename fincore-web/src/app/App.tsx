import { BrowserRouter } from 'react-router-dom';
import Providers from './providers';
import AppRouter from './router';

export default function App() {
  return <Providers><BrowserRouter><AppRouter /></BrowserRouter></Providers>;
}
