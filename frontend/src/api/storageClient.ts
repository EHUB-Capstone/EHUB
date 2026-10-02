import axios from 'axios';

// Client for presigned object-storage URLs (Cloudflare R2). It is deliberately separate from
// axiosClient: no baseURL, no cookies, no interceptors and never an Authorization header, so the
// API access token is not sent to a third-party host.
const storageClient = axios.create({ withCredentials: false });

export default storageClient;
