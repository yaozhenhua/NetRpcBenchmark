// Copyright (C) 2018 Zhenhua Yao. All rights reserved.

package main

import (
	"fmt"
	"log"
	"net"

	pb "./EchoServer"
	"golang.org/x/net/context"
	"google.golang.org/grpc"
	"google.golang.org/grpc/reflection"
)

const (
	port = ":12358"
)

type server struct{}

func (s *server) Echo(ctx context.Context, in *pb.Message) (*pb.Message, error) {
	return &pb.Message{Text: in.Text}, nil
}

func main() {
	lis, err := net.Listen("tcp", port)
	if err != nil {
		log.Fatalf("Failed to listne: %v", err)
	}

	s := grpc.NewServer()
	pb.RegisterEchoServer(s, &server{})

	reflection.Register(s)
	fmt.Printf("Go gRPC server running at port %s", port)
	if err := s.Serve(lis); err != nil {
		log.Fatalf("Failed to serve: %v", err)
	}
}
