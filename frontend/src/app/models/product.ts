export interface Product {
  id: number;
  name: string;
  category: string;
  price: number;
  sizes: string[];
  imageUrl: string | null;
}
