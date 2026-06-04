package com.java.aistudyhubbe.repository;

import com.java.aistudyhubbe.entity.CloudFile;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.UUID;

@Repository
public interface CloudFileRepository extends JpaRepository<CloudFile, UUID> {
}
